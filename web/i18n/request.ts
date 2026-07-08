import { getRequestConfig } from "next-intl/server";
import { cookies, headers } from "next/headers";

const SUPPORTED_LOCALES = ["nl", "fr", "en"];
const DEFAULT_LOCALE = "nl";

/**
 * No [locale] URL segment (design-decisions.md/spec 007a Assumptions: no locale-switcher UI in
 * this feature) — resolution order is an explicit cookie (future switcher writes here), then
 * the browser's Accept-Language header, then the product-wide default ("nl", matching
 * mobile/i18n's fallback).
 */
async function resolveLocale(): Promise<string> {
  const cookieLocale = (await cookies()).get("locale")?.value;
  if (cookieLocale && SUPPORTED_LOCALES.includes(cookieLocale)) return cookieLocale;

  const acceptLanguage = (await headers()).get("accept-language");
  const preferred = acceptLanguage?.split(",")[0]?.split("-")[0];
  if (preferred && SUPPORTED_LOCALES.includes(preferred)) return preferred;

  return DEFAULT_LOCALE;
}

export default getRequestConfig(async () => {
  const locale = await resolveLocale();
  const messages = (await import(`./locales/${locale}.json`)).default;
  return { locale, messages };
});
