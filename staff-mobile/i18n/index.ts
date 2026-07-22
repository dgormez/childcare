import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import * as Localization from "expo-localization";

import nl from "./locales/nl.json";
import fr from "./locales/fr.json";
import en from "./locales/en.json";

const SUPPORTED_LANGUAGES = ["nl", "fr", "en"];

function resolveDeviceLanguage(): string {
  const tag = Localization.getLocales()[0]?.languageCode ?? "nl";
  return SUPPORTED_LANGUAGES.includes(tag) ? tag : "nl";
}

i18n.use(initReactI18next).init({
  resources: {
    nl: { translation: nl },
    fr: { translation: fr },
    en: { translation: en },
  },
  lng: resolveDeviceLanguage(),
  fallbackLng: "nl",
  interpolation: { escapeValue: false },
});

export default i18n;
