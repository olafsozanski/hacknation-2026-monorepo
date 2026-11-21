import type {
  DocumentKind,
  KnowledgeKind,
  RedactionCategory,
  RedactionSource,
  ReplyChannel,
  RequestStatus,
  SuggestionSource,
  Urgency,
} from '@/api/types'
import { days } from './dates'

export const statusLabels: Record<RequestStatus, string> = {
  NoAction: 'Brak działań',
  QueriesSent: 'Zapytania wysłane do wydziałów',
  Extended: 'Kwerenda — termin przedłużony',
  ResponsePrepared: 'Odpowiedź przygotowana',
  ResponseSent: 'Odpowiedź wysłana',
}

export const statusShortLabels: Record<RequestStatus, string> = {
  NoAction: 'Brak działań',
  QueriesSent: 'W wydziałach',
  Extended: 'Kwerenda',
  ResponsePrepared: 'Odpowiedź gotowa',
  ResponseSent: 'Wysłano',
}

export const statusOrder: RequestStatus[] = ['NoAction', 'QueriesSent', 'Extended', 'ResponsePrepared', 'ResponseSent']

export const replyChannelLabels: Record<ReplyChannel, string> = {
  Email: 'E-mail',
  Post: 'Poczta tradycyjna',
  Epuap: 'ePUAP / e-Doręczenia',
  InPerson: 'Odbiór osobisty',
  Other: 'Inna',
}

export const documentKindLabels: Record<DocumentKind, string> = {
  Request: 'Wniosek',
  DepartmentReply: 'Odpowiedź wydziału',
  Attachment: 'Załącznik',
}

export const knowledgeKindLabels: Record<KnowledgeKind, string> = {
  Law: 'Akt prawny',
  Ordinance: 'Zarządzenie',
  ArchivedResponse: 'Archiwalna odpowiedź',
  Guideline: 'Wytyczne',
  Other: 'Inne',
}

export const redactionCategoryLabels: Record<RedactionCategory, string> = {
  PersonName: 'Imię i nazwisko',
  Address: 'Adres',
  Pesel: 'PESEL',
  IdDocument: 'Nr dokumentu tożsamości',
  Phone: 'Telefon',
  Email: 'E-mail',
  BankAccount: 'Rachunek bankowy',
  Nip: 'NIP',
  DateOfBirth: 'Data urodzenia',
  Other: 'Inne dane',
}

export const redactionSourceLabels: Record<RedactionSource, string> = {
  Rule: 'Reguła',
  Ai: 'AI',
  Manual: 'Ręcznie',
}

export const suggestionSourceLabels: Record<SuggestionSource, string> = {
  Ai: 'Analiza AI',
  History: 'Historia spraw',
  Keywords: 'Zakres działania',
}

export const auditActionLabels: Record<string, string> = {
  'request.created': 'Zarejestrowano wniosek',
  'request.updated': 'Zmieniono dane sprawy',
  'request.status-changed': 'Zmieniono status',
  'request.extended': 'Przedłużono termin (kwerenda)',
  'request.departments-changed': 'Zmieniono wydziały',
  'request.department-updated': 'Zaktualizowano zapytanie do wydziału',
  'document.uploaded': 'Dodano dokument',
  'document.deleted': 'Usunięto dokument',
  'draft.generated': 'Wygenerowano projekt odpowiedzi',
  'draft.saved': 'Zapisano projekt odpowiedzi',
  'draft.exported': 'Pobrano odpowiedź',
}

export function deadlineLabel(urgency: Urgency, daysLeft: number) {
  switch (urgency) {
    case 'Overdue':
      return `${days(-daysLeft)} po terminie`
    case 'DueToday':
      return 'Termin dziś'
    case 'DueTomorrow':
      return 'Termin jutro'
    case 'Closed':
      return 'Zakończona'
    default:
      return `Za ${days(daysLeft)}`
  }
}

export function similarityLabel(score: number) {
  if (score >= 0.66) return 'wysokie'
  if (score >= 0.33) return 'średnie'
  return 'niskie'
}
