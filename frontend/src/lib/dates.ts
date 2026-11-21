import type { IsoDate } from '@/api/types'

const shortFormat = new Intl.DateTimeFormat('pl-PL', { day: '2-digit', month: '2-digit', year: 'numeric' })
const longFormat = new Intl.DateTimeFormat('pl-PL', { day: 'numeric', month: 'long', year: 'numeric' })
const dateTimeFormat = new Intl.DateTimeFormat('pl-PL', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})
const timeFormat = new Intl.DateTimeFormat('pl-PL', { hour: '2-digit', minute: '2-digit' })
const monthFormat = new Intl.DateTimeFormat('pl-PL', { month: 'long', year: 'numeric' })

export function parseIsoDate(value: IsoDate): Date {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day)
}

export function toIsoDate(date: Date): IsoDate {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

export function todayIso(): IsoDate {
  return toIsoDate(new Date())
}

export const formatDate = (value: IsoDate | null | undefined) => (value ? shortFormat.format(parseIsoDate(value)) : '—')
export const formatLongDate = (value: IsoDate) => longFormat.format(parseIsoDate(value))
export const formatDateTime = (value: string) => dateTimeFormat.format(new Date(value))
export const formatTime = (value: Date) => timeFormat.format(value)
export const formatMonth = (value: Date) => monthFormat.format(value)

export function plural(count: number, one: string, few: string, many: string) {
  if (count === 1) return one
  const lastDigit = count % 10
  const lastTwo = count % 100
  return lastDigit >= 2 && lastDigit <= 4 && (lastTwo < 12 || lastTwo > 14) ? few : many
}

export const days = (count: number) => `${count} ${count === 1 ? 'dzień' : 'dni'}`
