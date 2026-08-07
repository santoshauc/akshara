import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';
import { API_BASE_URL } from '../config';
import { AuthTokens } from './types';

const ACCESS_KEY = 'schoolerp.access';
const REFRESH_KEY = 'schoolerp.refresh';

/**
 * Token storage: SecureStore (Keychain/Keystore) on device, localStorage on
 * web where SecureStore is unavailable.
 */
async function getItem(key: string): Promise<string | null> {
  if (Platform.OS === 'web') {
    return typeof localStorage === 'undefined' ? null : localStorage.getItem(key);
  }
  return SecureStore.getItemAsync(key);
}

async function setItem(key: string, value: string): Promise<void> {
  if (Platform.OS === 'web') {
    localStorage.setItem(key, value);
    return;
  }
  await SecureStore.setItemAsync(key, value);
}

async function removeItem(key: string): Promise<void> {
  if (Platform.OS === 'web') {
    localStorage.removeItem(key);
    return;
  }
  await SecureStore.deleteItemAsync(key);
}

export const tokenStore = {
  getAccess: () => getItem(ACCESS_KEY),
  getRefresh: () => getItem(REFRESH_KEY),
  async set(tokens: AuthTokens): Promise<void> {
    await setItem(ACCESS_KEY, tokens.accessToken);
    await setItem(REFRESH_KEY, tokens.refreshToken);
  },
  async clear(): Promise<void> {
    await removeItem(ACCESS_KEY);
    await removeItem(REFRESH_KEY);
  },
};

/** Raised for non-2xx responses, carrying the problem title when present. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

async function readProblemTitle(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as {
      title?: string;
      errors?: Record<string, string[]>;
    };
    if (problem.errors) {
      return Object.values(problem.errors).flat().join(' ');
    }
    return problem.title ?? `Request failed (${response.status}).`;
  } catch {
    return `Request failed (${response.status}).`;
  }
}

async function tryRefresh(): Promise<boolean> {
  const refreshToken = await tokenStore.getRefresh();
  if (!refreshToken) {
    return false;
  }

  const response = await fetch(`${API_BASE_URL}/api/v1/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  });
  if (!response.ok) {
    await tokenStore.clear();
    return false;
  }

  const tokens = (await response.json()) as AuthTokens;
  await tokenStore.set(tokens);
  return true;
}

/**
 * Authenticated JSON request with a single transparent token refresh on 401 —
 * the same session discipline the web portal uses.
 */
export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const send = async (): Promise<Response> => {
    const access = await tokenStore.getAccess();
    return fetch(`${API_BASE_URL}${path}`, {
      ...init,
      headers: {
        'Content-Type': 'application/json',
        ...(access ? { Authorization: `Bearer ${access}` } : {}),
        ...(init?.headers ?? {}),
      },
    });
  };

  let response = await send();
  if (response.status === 401 && (await tryRefresh())) {
    response = await send();
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblemTitle(response));
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
