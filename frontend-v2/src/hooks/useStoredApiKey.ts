'use client';

import { useState, useEffect, useCallback } from 'react';
import { getStoredApiKey, setStoredApiKey } from '@/lib/api/client';

const API_KEY_CHANGED_EVENT = 'smk:api-key-changed';

/**
 * SSoT for the locally-stored X-API-Key (`smk_api_key`).
 * Every consumer stays in sync via a window CustomEvent, so saving the key
 * in one component (e.g. Topbar) is reflected immediately in others (e.g. AppShell).
 */
export function useStoredApiKey() {
  const [apiKey, setApiKeyState] = useState('');

  useEffect(() => {
    setApiKeyState(getStoredApiKey());

    const handleChange = () => setApiKeyState(getStoredApiKey());
    window.addEventListener(API_KEY_CHANGED_EVENT, handleChange);
    return () => window.removeEventListener(API_KEY_CHANGED_EVENT, handleChange);
  }, []);

  const saveApiKey = useCallback((key: string) => {
    setStoredApiKey(key);
    setApiKeyState(key.trim());
    window.dispatchEvent(new CustomEvent(API_KEY_CHANGED_EVENT));
  }, []);

  return { apiKey, hasApiKey: apiKey.length > 0, saveApiKey };
}
