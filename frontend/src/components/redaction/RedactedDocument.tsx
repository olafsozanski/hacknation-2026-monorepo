import { Box, Button, chakra } from '@chakra-ui/react'
import { EyeOff } from 'lucide-react'
import { useRef, useState, type MouseEvent } from 'react'
import type { Redaction } from '@/api/types'
import { redactionCategoryLabels } from '@/lib/labels'
import { buildSegments, offsetWithin, splitPlaceholders } from './redactions'

export type DocumentMode = 'review' | 'preview'

type RedactedDocumentProps = {
  content: string
  redactions: Redaction[]
  mode: DocumentMode
  label: string
  onToggle: (index: number) => void
  onSelectRange: (start: number, end: number) => void
}

const Mark = chakra('button')

function TextPieces({ text, start }: { text: string; start: number }) {
  return (
    <>
      {splitPlaceholders(text, start).map((piece) =>
        piece.placeholder ? (
          <chakra.mark key={piece.start} data-offset={piece.start} bg="yellow.subtle" color="yellow.fg" px="0.5" borderRadius="xs" fontWeight="semibold">
            {piece.text}
          </chakra.mark>
        ) : (
          <span key={piece.start} data-offset={piece.start}>
            {piece.text}
          </span>
        ),
      )}
    </>
  )
}

export function RedactedDocument({ content, redactions, mode, label, onToggle, onSelectRange }: RedactedDocumentProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const [selection, setSelection] = useState<{ start: number; end: number; top: number; left: number } | null>(null)

  const captureSelection = (event: MouseEvent) => {
    if (mode !== 'review' || !containerRef.current) return
    if ((event.target as HTMLElement).closest('[data-selection-action]')) return
    const current = window.getSelection()
    if (!current || current.isCollapsed || current.rangeCount === 0) {
      setSelection(null)
      return
    }
    const range = current.getRangeAt(0)
    const start = offsetWithin(containerRef.current, range.startContainer, range.startOffset)
    const end = offsetWithin(containerRef.current, range.endContainer, range.endOffset)
    if (start === null || end === null || start === end) {
      setSelection(null)
      return
    }
    const box = range.getBoundingClientRect()
    const host = containerRef.current.getBoundingClientRect()
    setSelection({ start, end, top: box.bottom - host.top + 6, left: Math.max(0, box.left - host.left) })
  }

  const segments = buildSegments(content, redactions)

  return (
    <Box position="relative" ref={containerRef} onMouseUp={captureSelection}>
      <Box
        as="article"
        aria-label={label}
        bg="bg.panel"
        borderWidth="1px"
        borderColor="border"
        borderRadius="md"
        boxShadow="sm"
        px={{ base: 5, md: 12 }}
        py={{ base: 6, md: 12 }}
        maxW="820px"
        mx="auto"
        whiteSpace="pre-wrap"
        lineHeight="1.8"
        fontSize="md"
        color="fg"
        wordBreak="break-word"
      >
        {segments.map((segment) => {
          if (segment.kind === 'text') return <TextPieces key={`t${segment.start}`} text={segment.text} start={segment.start} />
          const { redaction, index } = segment
          const category = redactionCategoryLabels[redaction.category]

          if (mode === 'preview') {
            return redaction.status === 'Approved' ? (
              <chakra.span key={`r${segment.start}`} bg="fg" color="bg" px="1" borderRadius="xs" fontSize="sm">
                [dane zanonimizowane]
              </chakra.span>
            ) : (
              <TextPieces key={`r${segment.start}`} text={segment.text} start={segment.start} />
            )
          }

          if (redaction.status === 'Rejected') {
            return <TextPieces key={`r${segment.start}`} text={segment.text} start={segment.start} />
          }

          const approved = redaction.status === 'Approved'
          return (
            <Mark
              key={`r${segment.start}`}
              type="button"
              id={`redaction-${index}`}
              data-offset={segment.start}
              aria-pressed={approved}
              aria-label={`${category}: ${segment.text}. ${approved ? 'Zostanie ukryte. Naciśnij, aby cofnąć.' : 'Do weryfikacji. Naciśnij, aby ukryć.'}`}
              title={`${category} — ${approved ? 'zostanie ukryte (kliknij, aby cofnąć)' : 'kliknij, aby ukryć'}`}
              onClick={() => onToggle(index)}
              display="inline"
              font="inherit"
              lineHeight="inherit"
              whiteSpace="pre-wrap"
              textAlign="start"
              cursor="pointer"
              px="0.5"
              mx="-0.5"
              borderRadius="xs"
              bg={approved ? 'fg' : 'bg.emphasized'}
              color={approved ? 'bg' : 'fg'}
              borderBottomWidth={approved ? 0 : '2px'}
              borderBottomStyle="dashed"
              borderBottomColor="fg.muted"
              boxDecorationBreak="clone"
              _hover={{ outline: '2px solid', outlineColor: 'focus.ring' }}
            >
              {segment.text}
            </Mark>
          )
        })}
      </Box>
      {selection && (
        <Button
          data-selection-action
          size="sm"
          position="absolute"
          top={`${selection.top}px`}
          left={`${selection.left}px`}
          zIndex="popover"
          boxShadow="md"
          onClick={() => {
            onSelectRange(selection.start, selection.end)
            setSelection(null)
            window.getSelection()?.removeAllRanges()
          }}
        >
          <EyeOff size={14} aria-hidden /> Ukryj zaznaczony fragment
        </Button>
      )}
    </Box>
  )
}
