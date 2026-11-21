import { Box, Button, Flex, Text } from '@chakra-ui/react'
import { ChevronDown, ChevronUp, Download } from 'lucide-react'
import { useState } from 'react'
import { downloadFile } from '@/api/client'
import type { RequestDetail } from '@/api/types'
import { LinkButton } from '@/components/common/LinkButton'
import { Panel } from '@/components/common/Panel'
import { notifyError } from '@/components/ui/toaster'

export function RequestContentPanel({ request }: { request: RequestDetail }) {
  const [expanded, setExpanded] = useState(false)
  const original = request.documents.find((d) => d.kind === 'Request')
  const long = request.content.length > 900

  return (
    <Panel
      title="Treść wniosku"
      actions={
        original && (
          <Button
            size="sm"
            variant="outline"
            onClick={() => downloadFile(`/api/documents/${original.id}/content`, original.fileName).catch((e) => notifyError(e))}
          >
            <Download size={14} aria-hidden /> Oryginał ({original.fileName})
          </Button>
        )
      }
    >
      {request.content ? (
        <>
          <Box position="relative">
            <Text
              id={`request-content-${request.id}`}
              whiteSpace="pre-wrap"
              lineHeight="1.7"
              maxH={long && !expanded ? '15rem' : undefined}
              overflow="hidden"
            >
              {request.content}
            </Text>
            {long && !expanded && (
              <Box position="absolute" insetX="0" bottom="0" h="16" bgGradient="to-t" gradientFrom="bg.panel" gradientTo="transparent" aria-hidden />
            )}
          </Box>
          {long && (
            <Flex mt="2">
              <LinkButton
                aria-expanded={expanded}
                aria-controls={`request-content-${request.id}`}
                onClick={() => setExpanded((e) => !e)}
              >
                {expanded ? <ChevronUp size={16} aria-hidden /> : <ChevronDown size={16} aria-hidden />}
                {expanded ? 'Zwiń treść' : 'Pokaż całą treść'}
              </LinkButton>
            </Flex>
          )}
        </>
      ) : (
        <Text color="fg.muted">Brak treści wniosku. Uzupełnij ją w danych sprawy.</Text>
      )}
    </Panel>
  )
}
