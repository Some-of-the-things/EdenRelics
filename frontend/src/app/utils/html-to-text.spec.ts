import { htmlToText } from './html-to-text';

describe('htmlToText', () => {
  it('strips tags and keeps the words', () => {
    expect(htmlToText('<p>A <em>1970s</em> wool dress</p>')).toBe('A 1970s wool dress');
  });

  it('turns breaks and paragraph ends into spaces rather than joining words', () => {
    expect(htmlToText('one<br>two<br/>three</p>four')).toBe('one two three four');
    expect(htmlToText('one<BR>two')).toBe('one two');
  });

  it('decodes the entities an authored description actually contains', () => {
    expect(htmlToText('Marks&nbsp;&amp;&nbsp;Spencer')).toBe('Marks & Spencer');
    expect(htmlToText('&quot;St Michael&quot;')).toBe('"St Michael"');
    expect(htmlToText('1970s &#39;prairie&#39; dress')).toBe("1970s 'prairie' dress");
  });

  it('does not decode an escaped entity twice', () => {
    // The bug this ordering exists to prevent: with `&amp;` decoded first, `&amp;lt;` became
    // `&lt;` and was then decoded again into `<`, so text meant to display "&lt;" turned into a
    // bracket. Ampersand is the escape character, so it is unescaped last.
    expect(htmlToText('&amp;lt;')).toBe('&lt;');
    expect(htmlToText('&amp;amp;')).toBe('&amp;');
  });

  it('collapses whitespace and trims', () => {
    expect(htmlToText('  <p>spaced\n\n   out</p>  ')).toBe('spaced out');
  });

  it('survives empty and tag-only input', () => {
    expect(htmlToText('')).toBe('');
    expect(htmlToText('<p></p>')).toBe('');
  });
});
