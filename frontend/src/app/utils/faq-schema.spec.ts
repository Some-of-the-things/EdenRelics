import {
  buildFaqPage,
  buildFaqPageFromHtml,
  extractFaqsFromHtml,
  MIN_DERIVED_FAQ_ENTRIES,
} from './faq-schema';

describe('buildFaqPage', () => {
  it('builds a FAQPage from hand-authored entries', () => {
    const page = buildFaqPage([{ question: 'Is it vintage?', answer: 'Yes, 1970s.' }]);
    expect(page).toEqual({
      '@type': 'FAQPage',
      mainEntity: [
        {
          '@type': 'Question',
          name: 'Is it vintage?',
          acceptedAnswer: { '@type': 'Answer', text: 'Yes, 1970s.' },
        },
      ],
    });
  });

  it('returns null rather than an empty FAQPage', () => {
    expect(buildFaqPage([])).toBeNull();
    expect(buildFaqPage([{ question: '  ', answer: 'orphaned' }])).toBeNull();
  });
});

describe('extractFaqsFromHtml', () => {
  const post = `
    <p>An intro paragraph that is not an answer to anything.</p>
    <h2>What is a prairie dress?</h2>
    <p>A prairie dress is a long dress, usually maxi and sometimes midi, characterised by romantic Victorian and pioneer-inspired styling.</p>
    <p>The style draws directly on 19th-century American frontier and English rural dress, reinterpreted through a romantic lens.</p>
    <h2>A short history of the prairie dress</h2>
    <p>Not phrased as a question, so it is not an FAQ entry.</p>
    <h2>Prairie, cottagecore, and folk: what's the difference?</h2>
    <p>These terms overlap and are often used interchangeably, but they describe subtly different things in practice.</p>
  `;

  it('takes only headings actually phrased as questions', () => {
    const faqs = extractFaqsFromHtml(post);
    expect(faqs.map((f) => f.question)).toEqual([
      'What is a prairie dress?',
      "Prairie, cottagecore, and folk: what's the difference?",
    ]);
  });

  it('answers with the prose under the heading, not the whole rest of the page', () => {
    const [first] = extractFaqsFromHtml(post);
    expect(first.answer).toContain('A prairie dress is a long dress');
    expect(first.answer).toContain('19th-century American frontier');
    expect(first.answer).not.toContain('Not phrased as a question');
  });

  it('ignores the layout tables the legacy email-HTML posts are built from', () => {
    const withCta = `
      <h2>Can you bleach rayon?</h2>
      <table><tr><td bgcolor="#b8903c"><a href="/shop">BROWSE THE SHOP</a></td></tr></table>
      <p>On thin, floaty or fragile vintage rayon, no. There is no dose of oxygen bleach that is safe for a fabric with no margin left.</p>
    `;
    const [entry] = extractFaqsFromHtml(withCta);
    expect(entry.answer).not.toContain('BROWSE THE SHOP');
    expect(entry.answer).toContain('There is no dose of oxygen bleach');
  });

  it('drops a question whose answer is too short to be one', () => {
    expect(extractFaqsFromHtml('<h2>Why?</h2><p>Because.</p>')).toEqual([]);
  });

  it('decodes entities so the schema carries readable text', () => {
    const html = '<h2>Prairie &amp; folk &mdash; same thing?</h2><p>They overlap, but folk is looser and less structured than prairie.</p>';
    const [entry] = extractFaqsFromHtml(html);
    expect(entry.question).toBe('Prairie & folk — same thing?');
  });

  it('handles missing or empty content', () => {
    expect(extractFaqsFromHtml(null)).toEqual([]);
    expect(extractFaqsFromHtml('')).toEqual([]);
    expect(extractFaqsFromHtml('<p>No headings at all.</p>')).toEqual([]);
  });

  describe('buildFaqPageFromHtml', () => {
    it('emits FAQPage once a post carries enough genuine Q&A', () => {
      const page = buildFaqPageFromHtml(post) as { mainEntity: unknown[] };
      expect(page).not.toBeNull();
      expect(page.mainEntity.length).toBe(2);
    });

    // A single Q&A is a heading that happens to end in a question mark, not an
    // FAQ — claiming otherwise overstates what the page offers.
    it('stays null below the threshold', () => {
      const one = '<h2>What is a prairie dress?</h2><p>A long dress with romantic Victorian and pioneer-inspired styling throughout.</p>';
      expect(extractFaqsFromHtml(one).length).toBeLessThan(MIN_DERIVED_FAQ_ENTRIES);
      expect(buildFaqPageFromHtml(one)).toBeNull();
    });
  });
});
