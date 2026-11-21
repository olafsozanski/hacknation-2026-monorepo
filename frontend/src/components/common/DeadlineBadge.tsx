import { Badge } from '@chakra-ui/react'
import { AlarmClock, AlertTriangle, CalendarCheck, CalendarClock, Clock, type LucideIcon } from 'lucide-react'
import type { Urgency } from '@/api/types'
import { deadlineLabel } from '@/lib/labels'

const appearance: Record<Urgency, { palette: string; variant: 'solid' | 'subtle' | 'outline'; icon: LucideIcon }> = {
  Overdue: { palette: 'red', variant: 'solid', icon: AlertTriangle },
  DueToday: { palette: 'red', variant: 'subtle', icon: AlarmClock },
  DueTomorrow: { palette: 'orange', variant: 'subtle', icon: AlarmClock },
  DueSoon: { palette: 'yellow', variant: 'subtle', icon: Clock },
  OnTrack: { palette: 'gray', variant: 'outline', icon: CalendarClock },
  Closed: { palette: 'green', variant: 'subtle', icon: CalendarCheck },
}

export function DeadlineBadge({ urgency, daysLeft }: { urgency: Urgency; daysLeft: number }) {
  const { palette, variant, icon: Icon } = appearance[urgency]
  return (
    <Badge colorPalette={palette} variant={variant} size="md" gap="1.5" px="2" py="0.5" whiteSpace="nowrap">
      <Icon size={14} aria-hidden strokeWidth={2.25} />
      {deadlineLabel(urgency, daysLeft)}
    </Badge>
  )
}
