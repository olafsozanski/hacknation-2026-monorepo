import { useEffect, useMemo, useState, type ReactNode } from 'react'
import {
  AccessibilityContext,
  textScales,
  type AccessibilityContextValue,
  type AccessibilityPreferences,
} from './accessibility'

const storageKey = 'udip.accessibility'
const defaults: AccessibilityPreferences = { textScale: 1, highContrast: false }

function readPreferences(): AccessibilityPreferences {
  try {
    const stored = JSON.parse(localStorage.getItem(storageKey) ?? 'null') as Partial<AccessibilityPreferences> | null
    return {
      textScale: textScales.find((s) => s === stored?.textScale) ?? defaults.textScale,
      highContrast: stored?.highContrast === true,
    }
  } catch {
    return defaults
  }
}

export function AccessibilityProvider({ children }: { children: ReactNode }) {
  const [preferences, setPreferences] = useState(readPreferences)

  useEffect(() => {
    const root = document.documentElement
    root.style.setProperty('--app-font-scale', `${preferences.textScale * 100}%`)
    if (preferences.highContrast) root.dataset.contrast = 'high'
    else delete root.dataset.contrast
    try {
      localStorage.setItem(storageKey, JSON.stringify(preferences))
    } catch {
      /* storage unavailable: preferences stay in memory */
    }
  }, [preferences])

  const value = useMemo<AccessibilityContextValue>(
    () => ({
      ...preferences,
      setTextScale: (textScale) => setPreferences((p) => ({ ...p, textScale })),
      setHighContrast: (highContrast) => setPreferences((p) => ({ ...p, highContrast })),
    }),
    [preferences],
  )

  return <AccessibilityContext.Provider value={value}>{children}</AccessibilityContext.Provider>
}
