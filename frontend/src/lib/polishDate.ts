import { parseDate, type DateValue } from '@chakra-ui/react'

const pad = (value: number) => String(value).padStart(2, '0')

export const formatPolishDate = (date: DateValue) => `${pad(date.day)}.${pad(date.month)}.${date.year}`

export function parsePolishDate(text: string): DateValue | undefined {
  const value = text.trim()
  const iso = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(value)
  const polish = /^(\d{1,2})[./-](\d{1,2})(?:[./-](\d{2}|\d{4}))?$/.exec(value)
  const [year, month, day] = iso
    ? [Number(iso[1]), Number(iso[2]), Number(iso[3])]
    : polish
      ? [polish[3] ? Number(polish[3].length === 2 ? `20${polish[3]}` : polish[3]) : new Date().getFullYear(), Number(polish[2]), Number(polish[1])]
      : [NaN, NaN, NaN]
  if (!year || month < 1 || month > 12 || day < 1 || day > 31) return undefined
  const candidate = new Date(year, month - 1, day)
  if (candidate.getMonth() !== month - 1) return undefined
  return parseDate(`${year}-${pad(month)}-${pad(day)}`)
}
