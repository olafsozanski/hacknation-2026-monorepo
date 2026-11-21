import { Badge } from '@chakra-ui/react'
import { CheckCircle2, CircleDashed, FileCheck2, Hourglass, Send, type LucideIcon } from 'lucide-react'
import type { RequestStatus } from '@/api/types'
import { statusLabels, statusShortLabels } from '@/lib/labels'

const appearance: Record<RequestStatus, { palette: string; icon: LucideIcon }> = {
  NoAction: { palette: 'gray', icon: CircleDashed },
  QueriesSent: { palette: 'blue', icon: Send },
  Extended: { palette: 'purple', icon: Hourglass },
  ResponsePrepared: { palette: 'teal', icon: FileCheck2 },
  ResponseSent: { palette: 'green', icon: CheckCircle2 },
}

type StatusBadgeProps = { status: RequestStatus; extensionDays?: number | null; short?: boolean }

export function StatusBadge({ status, extensionDays, short }: StatusBadgeProps) {
  const { palette, icon: Icon } = appearance[status]
  const label = short ? statusShortLabels[status] : statusLabels[status]
  const suffix = status === 'Extended' && extensionDays ? ` +${extensionDays} dni` : ''
  return (
    <Badge colorPalette={palette} variant="subtle" size="md" gap="1.5" px="2" py="0.5" whiteSpace="nowrap">
      <Icon size={14} aria-hidden strokeWidth={2.25} />
      {label}
      {suffix}
    </Badge>
  )
}
