import { Box, Button, Card, Flex, Input, InputGroup, SegmentGroup, Stack, Text, chakra } from '@chakra-ui/react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Search, SearchX } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router'
import { api, query } from '@/api/client'
import type { SearchHit, SearchHitKind } from '@/api/types'
import { PageHeader } from '@/components/common/PageHeader'
import { EmptyHint, ErrorState, LoadingState } from '@/components/common/States'
import { SimilarCasesList } from '@/components/domain/SimilarCasesList'
import { plural } from '@/lib/dates'

type Scope = 'all' | SearchHitKind

export function ArchivePage() {
  const [params, setParams] = useSearchParams()
  const q = params.get('q') ?? ''
  const scope = (params.get('zakres') as Scope | null) ?? 'all'
  const [draft, setDraft] = useState(q)

  const results = useQuery({
    queryKey: ['search', q, scope],
    queryFn: () => api<SearchHit[]>(`/api/search${query({ q, limit: 30, kind: scope === 'all' ? undefined : scope })}`),
    enabled: q.length > 0,
    placeholderData: keepPreviousData,
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const next = new URLSearchParams(params)
    if (draft.trim()) next.set('q', draft.trim())
    else next.delete('q')
    setParams(next)
  }

  const setScope = (value: Scope) => {
    const next = new URLSearchParams(params)
    if (value === 'all') next.delete('zakres')
    else next.set('zakres', value)
    setParams(next, { replace: true })
  }

  return (
    <>
      <PageHeader
        title="Archiwum spraw"
        description="Przeszukuj zarejestrowane wnioski, udzielone odpowiedzi oraz dokumenty z bazy wiedzy. Wyszukiwanie uwzględnia podobieństwo treści, a nie tylko dokładne słowa."
      />
      <Stack gap="4">
        <Card.Root variant="outline" bg="bg.panel">
          <Card.Body gap="3">
            <chakra.form onSubmit={submit} role="search" aria-label="Wyszukiwanie w archiwum">
              <Flex gap="2" direction={{ base: 'column', sm: 'row' }}>
                <InputGroup startElement={<Search size={18} aria-hidden />} flex="1">
                  <Input
                    size="lg"
                    type="search"
                    aria-label="Czego szukasz?"
                    placeholder="Np. koszty wycinki drzew, umowa na oczyszczanie miasta"
                    value={draft}
                    onChange={(e) => setDraft(e.target.value)}
                  />
                </InputGroup>
                <Button type="submit" size="lg">
                  Szukaj
                </Button>
              </Flex>
            </chakra.form>
            <SegmentGroup.Root value={scope} onValueChange={(e) => setScope(e.value as Scope)} aria-label="Zakres wyszukiwania" size="sm" alignSelf="flex-start">
              <SegmentGroup.Indicator />
              <SegmentGroup.Items
                items={[
                  { value: 'all', label: 'Wszystko' },
                  { value: 'Request', label: 'Wnioski i odpowiedzi' },
                  { value: 'Knowledge', label: 'Baza wiedzy' },
                ]}
              />
            </SegmentGroup.Root>
          </Card.Body>
        </Card.Root>

        {!q ? (
          <EmptyHint
            icon={<Search aria-hidden />}
            title="Wpisz, czego szukasz"
            description="Opisz temat własnymi słowami — asystent znajdzie także sprawy sformułowane inaczej."
          />
        ) : results.isPending ? (
          <LoadingState label="Wyszukiwanie…" />
        ) : results.isError ? (
          <ErrorState error={results.error} onRetry={() => results.refetch()} />
        ) : results.data.length === 0 ? (
          <EmptyHint icon={<SearchX aria-hidden />} title="Brak wyników" description="Spróbuj innych słów lub szerszego zakresu." />
        ) : (
          <Box>
            <Text color="fg.muted" fontSize="sm" mb="2" role="status" aria-live="polite">
              Znaleziono {results.data.length} {plural(results.data.length, 'wynik', 'wyniki', 'wyników')}
            </Text>
            <Card.Root variant="outline" bg="bg.panel">
              <Card.Body>
                <SimilarCasesList hits={results.data} />
              </Card.Body>
            </Card.Root>
          </Box>
        )}
      </Stack>
    </>
  )
}
