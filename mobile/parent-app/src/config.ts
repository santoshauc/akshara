import { Platform } from 'react-native';

/**
 * API base URL.
 * - Android emulator reaches the host machine via 10.0.2.2.
 * - iOS simulator and web use localhost.
 * - On a physical device, replace with your machine's LAN IP.
 */
export const API_BASE_URL =
  Platform.OS === 'android' ? 'http://10.0.2.2:5199' : 'http://localhost:5199';

/** Brand color shared across screens. */
export const BRAND = '#1565C0';
