import { describe, expect, it } from 'vitest'
import { suggestedQuestions } from './suggestions'

describe('suggestedQuestions', () => {
  it('suggests case questions on a case and its response draft', () => {
    expect(suggestedQuestions('/wnioski/5f0c', false)).toEqual([
      'Co mam teraz zrobić w tej sprawie?',
      'Do kiedy mogę przedłużyć termin?',
      'Gdzie zarejestruję nowy wniosek?',
      'Jak liczony jest termin odpowiedzi?',
    ])
    expect(suggestedQuestions('/wnioski/5f0c/odpowiedz', false)[0]).toBe('Na co zwrócić uwagę przed wysłaniem odpowiedzi?')
  })

  it('puts the form question first when the screen has invalid fields', () => {
    expect(suggestedQuestions('/wnioski/5f0c', true)[0]).toBe('Dlaczego nie mogę zapisać formularza?')
  })

  it('treats the registration form as a form, not as a case', () => {
    expect(suggestedQuestions('/wnioski/nowy?krok=2', false)).toEqual([
      'Dlaczego nie mogę zapisać formularza?',
      'Jak liczony jest termin odpowiedzi?',
    ])
  })

  it('falls back to general questions', () => {
    expect(suggestedQuestions('/baza-wiedzy', false)).toEqual(['Gdzie zarejestruję nowy wniosek?', 'Jak liczony jest termin odpowiedzi?'])
  })
})
