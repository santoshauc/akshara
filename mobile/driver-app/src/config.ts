import { Platform } from 'react-native';

/**
 * API base URL.
 * - Android emulator reaches the host machine via 10.0.2.2.
 * - iOS simulator and web use localhost.
 * - On a physical device, replace with your machine's LAN IP.
 */
export const API_BASE_URL =
  Platform.OS === 'android' ? 'http://10.0.2.2:5199' : 'http://localhost:5199';

/** Driver-app brand color (amber, distinct from the parent app's blue). */
export const BRAND = '#E65100';

/** Seconds between GPS pings while a trip is active. */
export const PING_INTERVAL_SECONDS = 20;
