/**
 * The smallest DOM the fill engine can be tested against.
 *
 * Deliberately not jsdom. The engine's contract is narrow — querySelector, a label sweep, and a
 * value setter that lives on the prototype so React notices it — and a fake that only implements
 * that contract makes it obvious when the engine starts depending on something wider.
 */

/** An input whose `value` setter is on the prototype, which is what `setValue` goes through. */
export class FakeInput extends EventTarget {
  #value = '';

  constructor(attributes = {}) {
    super();
    this.attributes = attributes;
    this.fired = [];
    this.addEventListener('input', (e) => this.fired.push(e.type));
    this.addEventListener('change', (e) => this.fired.push(e.type));
  }

  get value() {
    return this.#value;
  }

  set value(next) {
    this.#value = next;
  }

  getAttribute(name) {
    return this.attributes[name] ?? null;
  }

  querySelector() {
    return null;
  }
}

export class FakeLabel {
  constructor(textContent, { forId = null, control = null } = {}) {
    this.textContent = textContent;
    this.forId = forId;
    this.control = control;
  }

  getAttribute(name) {
    return name === 'for' ? this.forId : null;
  }

  querySelector() {
    return this.control;
  }
}

export class FakeRoot {
  /**
   * @param {Record<string, object>} selectors Selector string → element.
   * @param {FakeLabel[]} labels
   */
  constructor(selectors = {}, labels = []) {
    this.selectors = selectors;
    this.labels = labels;
  }

  querySelector(selector) {
    return this.selectors[selector] ?? null;
  }

  querySelectorAll(selector) {
    return selector === 'label' ? this.labels : [];
  }
}

/**
 * A clock that only moves when something waits on it.
 *
 * Lets the retry loops in `waitFor` reach their deadline instantly instead of holding the suite up
 * for fifteen real seconds per missing field.
 */
export function fakeClock(start = 0) {
  let current = start;
  return {
    now: () => current,
    timer: (fn, ms = 0) => {
      current += ms;
      fn();
      return 0;
    },
    advance: (ms) => {
      current += ms;
    },
  };
}
