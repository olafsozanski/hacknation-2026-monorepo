import { Group, IconButton } from '@chakra-ui/react'
import { Contrast } from 'lucide-react'
import { textScales, useAccessibility } from '@/app/accessibility'

const scaleLabels = ['Mniejszy tekst', 'Domyślny rozmiar tekstu', 'Większy tekst', 'Największy tekst']
const scaleGlyphs = ['A−', 'A', 'A+', 'A++']

export function A11yControls() {
  const { textScale, setTextScale, highContrast, setHighContrast } = useAccessibility()
  return (
    <Group attached role="group" aria-label="Ustawienia dostępności">
      {textScales.map((scale, index) => (
        <IconButton
          key={scale}
          size="sm"
          variant={textScale === scale ? 'subtle' : 'ghost'}
          aria-label={scaleLabels[index]}
          aria-pressed={textScale === scale}
          onClick={() => setTextScale(scale)}
          fontWeight="bold"
          fontSize={`${0.7 + index * 0.1}rem`}
          minW="8"
          hideBelow={index === 3 ? 'xl' : undefined}
        >
          {scaleGlyphs[index]}
        </IconButton>
      ))}
      <IconButton
        size="sm"
        variant={highContrast ? 'subtle' : 'ghost'}
        aria-label="Wersja kontrastowa"
        aria-pressed={highContrast}
        onClick={() => setHighContrast(!highContrast)}
      >
        <Contrast size={18} aria-hidden />
      </IconButton>
    </Group>
  )
}
