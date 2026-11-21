import type { Urgency } from '@/api/types'

export const urgencyRowStyle: Partial<Record<Urgency, { accent: string; bg: string }>> = {
  Overdue: { accent: 'red.solid', bg: 'red.subtle' },
  DueToday: { accent: 'red.solid', bg: 'red.subtle' },
  DueTomorrow: { accent: 'orange.solid', bg: 'orange.subtle' },
}
