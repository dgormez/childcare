// https://nl.wikipedia.org/wiki/Rijksregisternummer — mirrors
// backend/ChildCare.Application/Children/RijksregisternummerChecksum.cs. The check (last 2 of
// the 11 digits) is 97 minus the first 9 digits mod 97; people born in 2000 or later compute it
// against the first 9 digits with "2" prefixed (+ 2_000_000_000) instead, since the 2-digit year
// alone can't tell the two centuries apart. Valid if either variant's check matches.
export function normalizeNrn(raw: string): string {
  return raw.replace(/[.\-\s]/g, "");
}

export function isValidNrnChecksum(elevenDigits: string): boolean {
  if (!/^\d{11}$/.test(elevenDigits)) return false;

  const first9 = Number(elevenDigits.slice(0, 9));
  const check = Number(elevenDigits.slice(9, 11));

  const beforeYear2000 = 97 - (first9 % 97);
  const fromYear2000 = 97 - ((2_000_000_000 + first9) % 97);

  return check === beforeYear2000 || check === fromYear2000;
}
