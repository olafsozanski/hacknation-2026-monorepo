import { createContext, useContext } from 'react'

export const textScales = [0.875, 1, 1.125, 1.25] as const
export type TextScale = (typeof textScales)[number]

export type AccessibilityPreferences = { textScale: TextScale; highContrast: boolean }

export type AccessibilityContextValue = AccessibilityPreferences & {
  setTextScale: (scale: TextScale) => void
  setHighContrast: (enabled: boolean) => void
}

export const AccessibilityContext = createContext<AccessibilityContextValue | null>(null)

export function useAccessibility() {
  const context = useContext(AccessibilityContext)
  if (!context) throw new Error('useAccessibility must be used within AccessibilityProvider.')
  return context
}
