import { collectErrors, type FieldErrors } from '@/lib/validation'
import type { RequestFormValues } from './RequestFormFields'

export function validateRequestForm(values: RequestFormValues, { dueOnRequired = true } = {}): FieldErrors {
  return collectErrors({
    subject: !values.subject.trim() && 'Podaj przedmiot wniosku.',
    receivedOn: !values.receivedOn && 'Podaj datę wpływu.',
    dueOn: dueOnRequired && !values.dueOn && 'Podaj termin odpowiedzi.',
  })
}
