import { Badge, Tooltip } from '@chakra-ui/react'
import { Cpu, Globe, PowerOff } from 'lucide-react'
import { useSystemInfo } from '@/features/platform/api'

export function AiModeBadge() {
  const { data } = useSystemInfo()
  if (!data) return null
  const { ai } = data

  const mode = !ai.enabled
    ? {
        palette: 'gray',
        icon: PowerOff,
        label: 'AI wyłączone',
        hint: 'Asystent działa w trybie podstawowym: wyszukiwanie pełnotekstowe i szablony pism.',
      }
    : ai.isLocal
      ? {
          palette: 'green',
          icon: Cpu,
          label: 'Model lokalny',
          hint: `Dane nie opuszczają sieci urzędu (model: ${ai.chatModel}).`,
        }
      : {
          palette: 'orange',
          icon: Globe,
          label: 'Model zewnętrzny',
          hint: `Tryb prototypu: treści są przetwarzane przez usługę ${ai.provider} (${ai.chatModel}). Nie używaj z prawdziwymi danymi osobowymi.`,
        }

  const Icon = mode.icon
  return (
    <Tooltip.Root openDelay={200}>
      <Tooltip.Trigger asChild>
        <Badge
          colorPalette={mode.palette}
          variant="subtle"
          size="lg"
          gap="1.5"
          tabIndex={0}
          aria-label={`${mode.label}. ${mode.hint}`}
          hideBelow="md"
        >
          <Icon size={14} aria-hidden />
          {mode.label}
        </Badge>
      </Tooltip.Trigger>
      <Tooltip.Positioner>
        <Tooltip.Content maxW="xs">{mode.hint}</Tooltip.Content>
      </Tooltip.Positioner>
    </Tooltip.Root>
  )
}
