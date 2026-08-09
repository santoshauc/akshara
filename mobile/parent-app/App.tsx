import { StatusBar } from 'expo-status-bar';
import React, { useEffect, useState } from 'react';
import { ActivityIndicator, View } from 'react-native';
import { tokenStore } from './src/api/client';
import { registerForPushAsync } from './src/api/push';
import { LanguageProvider } from './src/i18n';
import { BRAND } from './src/config';
import HomeScreen from './src/screens/HomeScreen';
import LoginScreen from './src/screens/LoginScreen';

/**
 * Root: restores the stored session and gates between Login and Home. The API
 * client transparently refreshes expired access tokens on first use.
 */
export default function App() {
  const [signedIn, setSignedIn] = useState<boolean | null>(null);

  useEffect(() => {
    void tokenStore.getRefresh().then((token) => setSignedIn(token != null));
  }, []);

  // Device push registration follows every sign-in (best-effort; no-op on web).
  useEffect(() => {
    if (signedIn) {
      void registerForPushAsync();
    }
  }, [signedIn]);

  if (signedIn === null) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
        <ActivityIndicator size="large" color={BRAND} />
      </View>
    );
  }

  return (
    <LanguageProvider signedIn={signedIn}>
      <StatusBar style="dark" />
      {signedIn ? (
        <HomeScreen onSignedOut={() => setSignedIn(false)} />
      ) : (
        <LoginScreen onSignedIn={() => setSignedIn(true)} />
      )}
    </LanguageProvider>
  );
}
