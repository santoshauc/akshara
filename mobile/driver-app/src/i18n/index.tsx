import * as SecureStore from 'expo-secure-store';
import React, { createContext, useContext, useEffect, useMemo, useState } from 'react';
import { Platform } from 'react-native';
import { en, te, TranslationKey } from './translations';

export type Language = 'en' | 'te';

const LANG_KEY = 'schoolerp.lang';

async function loadLanguage(): Promise<Language> {
  const stored =
    Platform.OS === 'web'
      ? typeof localStorage === 'undefined'
        ? null
        : localStorage.getItem(LANG_KEY)
      : await SecureStore.getItemAsync(LANG_KEY);
  return stored === 'te' ? 'te' : 'en';
}

async function saveLanguage(lang: Language): Promise<void> {
  if (Platform.OS === 'web') {
    localStorage.setItem(LANG_KEY, lang);
    return;
  }
  await SecureStore.setItemAsync(LANG_KEY, lang);
}

interface I18n {
  lang: Language;
  setLang: (lang: Language) => void;
  t: (key: TranslationKey, params?: Record<string, string | number>) => string;
}

const I18nContext = createContext<I18n | null>(null);

/** Loads the saved language once, then provides t() to the whole app. */
export function LanguageProvider({ children }: { children: React.ReactNode }) {
  const [lang, setLangState] = useState<Language>('en');

  useEffect(() => {
    void loadLanguage().then(setLangState);
  }, []);

  const value = useMemo<I18n>(() => {
    const table = lang === 'te' ? te : en;
    return {
      lang,
      setLang: (next) => {
        setLangState(next);
        void saveLanguage(next);
      },
      t: (key, params) => {
        let text: string = table[key];
        if (params) {
          for (const [name, replacement] of Object.entries(params)) {
            text = text.replace(`{${name}}`, String(replacement));
          }
        }
        return text;
      },
    };
  }, [lang]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

/** Translation hook; must be used under LanguageProvider. */
export function useI18n(): I18n {
  const context = useContext(I18nContext);
  if (!context) {
    throw new Error('useI18n must be used inside LanguageProvider');
  }
  return context;
}
