import { Platform } from 'react-native';
import { request } from './client';

/**
 * Registers this device for push notifications: asks permission, fetches the
 * Expo push token and hands it to the API. Silently no-ops where push isn't
 * available (web, simulators, missing EAS project id) — SMS remains the
 * fallback channel for every guardian.
 */
export async function registerForPushAsync(): Promise<void> {
  if (Platform.OS === 'web') {
    return;
  }
  try {
    const Device = await import('expo-device');
    if (!Device.isDevice) {
      return;
    }

    const Notifications = await import('expo-notifications');
    const { status: existing } = await Notifications.getPermissionsAsync();
    let status = existing;
    if (existing !== 'granted') {
      ({ status } = await Notifications.requestPermissionsAsync());
    }
    if (status !== 'granted') {
      return;
    }

    const token = (await Notifications.getExpoPushTokenAsync()).data;
    await request<void>('/api/v1/push/tokens', {
      method: 'POST',
      body: JSON.stringify({ token, platform: Platform.OS }),
    });
  } catch {
    // Push is best-effort; never block the app over it.
  }
}
