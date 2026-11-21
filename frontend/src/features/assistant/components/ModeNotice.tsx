import { Stack } from '@chakra-ui/react'
import { Info, PowerOff, type LucideIcon } from 'lucide-react'
import type { SystemInfo } from '@/api/types'
import { useSystemInfo } from '@/features/platform/api'
import { NoticeLine } from './NoticeLine'

type Notice = { icon: LucideIcon; text: string }

function noticesFor({ ai }: SystemInfo): Notice[] {
  if (!ai.enabled)
    return [{ icon: PowerOff, text: 'Model AI jest wyłączony — asystent odpowiada w trybie podstawowym i nie proponuje działań.' }]
  if (!ai.assistantTools)
    return [
      {
        icon: Info,
        text: 'W tym trybie asystent nie proponuje działań — odpowiada na podstawie przewodnika po aplikacji, bieżącego ekranu i wyszukiwania w archiwum.',
      },
    ]
  return []
}

export function ModeNotice() {
  const { data } = useSystemInfo()
  const notices = data ? noticesFor(data) : []
  if (notices.length === 0) return null
  return (
    <Stack gap="1.5" px="4" py="2.5" bg="bg.subtle" borderBottomWidth="1px" borderColor="border">
      {notices.map(({ icon, text }) => (
        <NoticeLine key={text} icon={icon}>
          {text}
        </NoticeLine>
      ))}
    </Stack>
  )
}
