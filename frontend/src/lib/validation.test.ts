import { describe, expect, it } from 'vitest'
import { collectErrors, hasErrors, isEmail } from './validation'

describe('collectErrors', () => {
  it('keeps only the rules that produced a message', () => {
    const errors = collectErrors({ name: 'Podaj nazwę.', email: false, phone: null, note: undefined })
    expect(errors).toEqual({ name: 'Podaj nazwę.' })
    expect(hasErrors(errors)).toBe(true)
  })

  it('reports no errors when every rule passes', () => {
    expect(hasErrors(collectErrors({ name: false }))).toBe(false)
  })
})

describe('isEmail', () => {
  it.each(['sekretariat@um.bydgoszcz.pl', 'a.b@c.de'])('accepts %s', (value) => expect(isEmail(value)).toBe(true))
  it.each(['', 'sekretariat', 'a@b', 'a b@c.pl', '@um.pl'])('rejects %s', (value) => expect(isEmail(value)).toBe(false))
})
