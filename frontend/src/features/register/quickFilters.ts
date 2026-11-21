import type { RequestListItem } from '@/api/types'

export type QuickFilter = 'overdue' | 'today' | 'tomorrow' | 'soon' | 'open' | 'closed'

export const quickFilterPredicates: Record<QuickFilter, (item: RequestListItem) => boolean> = {
  overdue: (item) => item.urgency === 'Overdue',
  today: (item) => item.urgency === 'DueToday',
  tomorrow: (item) => item.urgency === 'DueTomorrow',
  soon: (item) => item.urgency === 'DueSoon',
  open: (item) => item.status !== 'ResponseSent',
  closed: (item) => item.status === 'ResponseSent',
}

export const isQuickFilter = (value: string | null): value is QuickFilter =>
  value !== null && value in quickFilterPredicates
