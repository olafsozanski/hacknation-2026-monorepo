import { afterEach, describe, expect, it } from 'vitest'
import { clip, collectScreenContext, hasInvalidFields, pathOnlyContext } from './screenContext'

const page = `
  <header><h1>Nagłówek aplikacji</h1></header>
  <main id="main-content">
    <h1>Rejestracja <span aria-hidden="true">✱</span>wniosku</h1>
    <div data-scope="field" data-part="root">
      <label data-scope="field" data-part="label" for="subject">Przedmiot wniosku <span aria-hidden="true">*</span></label>
      <textarea id="subject" aria-invalid="true" aria-describedby="subject-error subject-help"></textarea>
      <div id="subject-error" data-scope="field" data-part="error-text">Podaj przedmiot wniosku.</div>
      <div id="subject-help" data-scope="field" data-part="helper-text">Krótki opis.</div>
    </div>
    <input aria-invalid="true" aria-label="Znak sprawy" aria-errormessage="case-error" />
    <p id="case-error">Znak sprawy jest zajęty.</p>
    <input aria-invalid="true" />
    <input aria-invalid="false" aria-label="Poprawne pole" />
    <div role="alert"><div>Nie udało się zapisać</div><div>Sprawdź pola formularza.</div></div>
    <div role="status"></div>
    <div hidden><div role="alert">Ukryty komunikat</div></div>
  </main>
  <span role="alert" style="position: absolute; clip: rect(0px, 0px, 0px, 0px); height: 1px; width: 1px">poniedziałek, 7 grudnia 2026</span>
  <span role="status" style="position: absolute; clip-path: inset(50%); height: 1px; width: 1px">Wybrano datę</span>
  <div role="dialog" data-state="open" aria-labelledby="dialog-title"><h2 id="dialog-title">Przedłużenie terminu</h2></div>
  <div role="dialog" data-state="closed" aria-label="Zamknięte okno"></div>
  <div data-assistant-ui>
    <div role="status">Asystent przygotowuje odpowiedź…</div>
    <textarea aria-invalid="true" aria-label="Twoje pytanie"></textarea>
  </div>
`

afterEach(() => {
  document.body.innerHTML = ''
  document.title = ''
})

describe('collectScreenContext', () => {
  it('describes what the clerk sees, excluding the assistant itself', () => {
    document.body.innerHTML = page
    document.title = 'Rejestracja wniosku – Asystent UDIP'

    expect(collectScreenContext('/wnioski/nowy?krok=2')).toEqual({
      path: '/wnioski/nowy?krok=2',
      title: 'Rejestracja wniosku – Asystent UDIP',
      heading: 'Rejestracja wniosku',
      dialog: 'Przedłużenie terminu',
      fieldErrors: [
        { label: 'Przedmiot wniosku', message: 'Podaj przedmiot wniosku.' },
        { label: 'Znak sprawy', message: 'Znak sprawy jest zajęty.' },
      ],
      alerts: ['Nie udało się zapisać Sprawdź pola formularza.'],
    })
  })

  it('returns empty details for an empty page', () => {
    expect(collectScreenContext('/')).toEqual({ path: '/', title: null, heading: null, dialog: null, fieldErrors: [], alerts: [] })
  })

  it('applies the contract limits', () => {
    const longText = 'x'.repeat(800)
    document.title = longText
    document.body.innerHTML = `<main id="main-content"><h1>${longText}</h1>${Array.from(
      { length: 25 },
      (_, i) => `<input aria-invalid="true" aria-label="Pole ${i}" /><div role="alert">Błąd ${i} ${longText}</div>`,
    ).join('')}</main>`

    const context = collectScreenContext(`/${longText}`)
    expect(context.path).toHaveLength(300)
    expect(context.title).toHaveLength(300)
    expect(context.heading).toHaveLength(300)
    expect(context.fieldErrors).toHaveLength(20)
    expect(context.fieldErrors[0]).toEqual({ label: 'Pole 0', message: 'Pole oznaczone jako niepoprawne.' })
    expect(context.alerts).toHaveLength(10)
    expect(context.alerts.every((alert) => alert.length === 500)).toBe(true)
  })
})

describe('hasInvalidFields', () => {
  it('ignores invalid fields inside the assistant', () => {
    document.body.innerHTML = '<div data-assistant-ui><textarea aria-invalid="true" aria-label="Twoje pytanie"></textarea></div>'
    expect(hasInvalidFields()).toBe(false)
    document.body.insertAdjacentHTML('beforeend', '<input aria-invalid="true" aria-label="Data wpływu" />')
    expect(hasInvalidFields()).toBe(true)
  })
})

describe('pathOnlyContext', () => {
  it('sends only the path', () => {
    expect(pathOnlyContext('/wnioski/abc')).toEqual({
      path: '/wnioski/abc',
      title: null,
      heading: null,
      dialog: null,
      fieldErrors: [],
      alerts: [],
    })
  })
})

describe('clip', () => {
  it('collapses whitespace and does not split surrogate pairs', () => {
    expect(clip('  a \n\t b  ', 10)).toBe('a b')
    expect(clip('ab😀', 3)).toBe('ab')
  })
})
