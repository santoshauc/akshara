import * as SecureStore from 'expo-secure-store';
import React, { createContext, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { Platform } from 'react-native';
import { parentApi } from '../api/parent';
import { en, te, TranslationKey } from './translations';

export type Language = 'en' | 'te';

const LANG_KEY = 'schoolerp.lang';

/** Null when this device has never been switched — nobody has chosen yet. */
async function loadLanguage(): Promise<Language | null> {
  const stored =
    Platform.OS === 'web'
      ? typeof localStorage === 'undefined'
        ? null
        : localStorage.getItem(LANG_KEY)
      : await SecureStore.getItemAsync(LANG_KEY);
  return stored === 'te' || stored === 'en' ? stored : null;
}

async function saveLanguage(lang: Language): Promise<void> {
  if (Platform.OS === 'web') {
    localStorage.setItem(LANG_KEY, lang);
    return;
  }
  await SecureStore.setItemAsync(LANG_KEY, lang);
}

/**
 * Mirrors the choice onto the guardian record so SMS, WhatsApp and push arrive
 * in the same language as the app. Best-effort: a parent who is offline still
 * gets their app translated, and the next sign-in re-syncs.
 */
async function pushLanguage(lang: Language): Promise<void> {
  try {
    await parentApi.setNotificationLanguage(lang);
  } catch {
    // Never let a notification preference break the UI toggle.
  }
}

interface I18n {
  lang: Language;
  setLang: (lang: Language) => void;
  t: (key: TranslationKey, params?: Record<string, string | number>) => string;
}

const I18nContext = createContext<I18n | null>(null);

/** Loads the saved language once, then provides t() to the whole app. */
export function LanguageProvider({
  children,
  signedIn = false,
}: {
  children: React.ReactNode;
  /** Enables the one-way sync with the guardian record once there is a session. */
  signedIn?: boolean;
}) {
  const [lang, setLangState] = useState<Language>('en');
  // null while the stored value is still being read.
  const [chosenHere, setChosenHere] = useState<boolean | null>(null);
  // The sign-in reconciliation runs once per session; later switches are
  // pushed by setLang itself, and without this guard flipping the toggle
  // would re-enter the effect and send the same PUT twice.
  const synced = useRef(false);

  useEffect(() => {
    void loadLanguage().then((stored) => {
      if (stored) {
        setLangState(stored);
      }
      setChosenHere(stored != null);
    });
  }, []);

  // At sign-in the two sides agree, and the reader always wins: a device that
  // has been switched pushes its choice; a fresh install adopts whatever the
  // school recorded, so an office-entered Telugu preference is not clobbered
  // back to English by a reinstall.
  useEffect(() => {
    if (!signedIn) {
      synced.current = false;
      return;
    }

    if (chosenHere === null || synced.current) {
      return;
    }

    synced.current = true;
    void (async () => {
      if (chosenHere) {
        await pushLanguage(lang);
        return;
      }

      try {
        const remote = await parentApi.getNotificationLanguage();
        if (remote.language === 'te' || remote.language === 'en') {
          setLangState(remote.language);
        }
      } catch {
        // Offline or a staff account: the local default stands.
      }
    })();
    // `lang` is deliberately absent: later switches push from setLang itself.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [signedIn, chosenHere]);

  const value = useMemo<I18n>(() => {
    const table = lang === 'te' ? te : en;
    return {
      lang,
      setLang: (next) => {
        setLangState(next);
        setChosenHere(true);
        void saveLanguage(next);
        void pushLanguage(next);
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
