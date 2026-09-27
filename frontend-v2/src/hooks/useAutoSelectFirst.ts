'use client';

import { useState, useEffect, Dispatch, SetStateAction } from 'react';

/**
 * Selects `initialKey` when given, otherwise defaults to the first item once
 * `items` loads. Used by views that pick a template via an optional
 * query-param id but otherwise want the first one preselected.
 */
export function useAutoSelectFirst<T>(
  items: T[],
  getKey: (item: T) => string,
  initialKey?: string
): [string, Dispatch<SetStateAction<string>>] {
  const [selectedKey, setSelectedKey] = useState(initialKey ?? '');

  useEffect(() => {
    if (initialKey) {
      setSelectedKey(initialKey);
    } else if (items.length > 0 && !selectedKey) {
      setSelectedKey(getKey(items[0]));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialKey, items, selectedKey]);

  return [selectedKey, setSelectedKey];
}
