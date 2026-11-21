import type { Urgency } from '@/api/types'

export const urgencyRowStyle: Partial<Record<Urgency, { accent: string; bg: string }>> = {
  Overdue: { accent: 'red.solid', bg: 'row.urgent' },
  DueToday: { accent: 'red.solid', bg: 'row.urgent' },
  DueTomorrow: { accent: 'orange.solid', bg: 'row.warning' },
}
