import { Text } from '@chakra-ui/react'
import { useQuery } from '@tanstack/react-query'
import { Panel } from '@/components/common/Panel'
import { SimilarCasesList } from '@/components/domain/SimilarCasesList'
import { similarQuery } from '../api'

export function SimilarCasesPanel({ requestId }: { requestId: string }) {
  const similar = useQuery(similarQuery(requestId))
  return (
    <Panel title="Podobne sprawy" description="Ostatnie 12 miesięcy i archiwum odpowiedzi.">
      {similar.isPending ? (
        <Text color="fg.muted">Wyszukiwanie…</Text>
      ) : similar.data && similar.data.length > 0 ? (
        <SimilarCasesList hits={similar.data} />
      ) : (
        <Text color="fg.muted" fontSize="sm">
          Nie znaleziono podobnych spraw.
        </Text>
      )}
    </Panel>
  )
}

