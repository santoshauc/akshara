import { StatusBar } from 'expo-status-bar';
import React, { useEffect, useState } from 'react';
import { ActivityIndicator, View } from 'react-native';
import { tokenStore } from './src/api/client';
import { BRAND } from './src/config';
import LoginScreen from './src/screens/LoginScreen';
import RouteScreen from './src/screens/RouteScreen';

/** Root: restores the stored session and gates between Login and the route. */
export default function App() {
  const [signedIn, setSignedIn] = useState<boolean | null>(null);

  useEffect(() => {
    void tokenStore.getRefresh().then((token) => setSignedIn(token != null));
  }, []);

  if (signedIn === null) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
        <ActivityIndicator size="large" color={BRAND} />
      </View>
    );
  }

  return (
    <>
      <StatusBar style="dark" />
      {signedIn ? (
        <RouteScreen onSignedOut={() => setSignedIn(false)} />
      ) : (
        <LoginScreen onSignedIn={() => setSignedIn(true)} />
      )}
    </>
  );
}
