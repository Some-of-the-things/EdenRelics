import { ImageLoader } from '@angular/common';

export const VARIANT_WIDTHS = [400, 800, 1200, 1600] as const;

const VARIANT_SUFFIX_REGEX = /-(\d+)\.webp$/;

export const variantImageLoader: ImageLoader = (config) => {
  if (!config.src) {
    return config.src;
  }
  if (!VARIANT_SUFFIX_REGEX.test(config.src)) {
    return config.src;
  }
  const requested = config.width ?? VARIANT_WIDTHS[VARIANT_WIDTHS.length - 1];
  const chosen = VARIANT_WIDTHS.find((w) => w >= requested) ?? VARIANT_WIDTHS[VARIANT_WIDTHS.length - 1];
  return config.src.replace(VARIANT_SUFFIX_REGEX, `-${chosen}.webp`);
};

export function imageSrcset(canonicalUrl: string | null | undefined): string | null {
  if (!canonicalUrl || !VARIANT_SUFFIX_REGEX.test(canonicalUrl)) {
    return null;
  }
  return VARIANT_WIDTHS
    .map((w) => `${canonicalUrl.replace(VARIANT_SUFFIX_REGEX, `-${w}.webp`)} ${w}w`)
    .join(', ');
}

export function imageSrcAt(canonicalUrl: string | null | undefined, width: number): string {
  if (!canonicalUrl) {
    return '';
  }
  if (!VARIANT_SUFFIX_REGEX.test(canonicalUrl)) {
    return canonicalUrl;
  }
  return canonicalUrl.replace(VARIANT_SUFFIX_REGEX, `-${width}.webp`);
}
