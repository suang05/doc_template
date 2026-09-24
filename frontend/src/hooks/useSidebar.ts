"use client";
import { useState, useEffect } from "react";

const STORAGE_KEY = "smk_sidebar_open";

export function useSidebar(defaultOpen = true) {
  const [open, setOpen] = useState(defaultOpen);

  useEffect(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved !== null) setOpen(saved === "true");
    } catch {}
  }, []);

  const toggle = () => {
    setOpen((prev) => {
      const next = !prev;
      try { localStorage.setItem(STORAGE_KEY, String(next)); } catch {}
      return next;
    });
  };

  return { open, toggle, setOpen };
}
