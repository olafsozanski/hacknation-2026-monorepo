import { useCallback, useState } from 'react'

export type FieldErrors = Record<string, string>

export function collectErrors(rules: Record<string, string | false | null | undefined>): FieldErrors {
  return Object.fromEntries(Object.entries(rules).filter((rule): rule is [string, string] => typeof rule[1] === 'string'))
}

export const hasErrors = (errors: FieldErrors) => Object.keys(errors).length > 0

export const isEmail = (value: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)

const focusableSelector = 'input:not([type=hidden]):not([type=file]), textarea, select, button'

export function focusFirstInvalid(form: HTMLElement) {
  requestAnimationFrame(() => {
    const field = form.querySelector('[data-scope="field"][data-part="root"][data-invalid]')
    const target = Array.from(field?.querySelectorAll<HTMLElement>(focusableSelector) ?? []).find(
      (element) => !element.hasAttribute('disabled') && element.getClientRects().length > 0,
    )
    target?.focus()
  })
}

export function useFieldErrors() {
  const [errors, setErrors] = useState<FieldErrors>({})
  const clearErrors = useCallback(
    (...fields: string[]) =>
      setErrors((current) =>
        fields.some((field) => field in current)
          ? Object.fromEntries(Object.entries(current).filter(([field]) => !fields.includes(field)))
          : current,
      ),
    [],
  )
  return [errors, setErrors, clearErrors] as const
}
