import { Box, Flex, HStack, Link, Progress, Stack, Tag, Text } from '@chakra-ui/react'
import { BookOpen, ExternalLink } from 'lucide-react'
import { Link as RouterLink } from 'react-router'
import type { SearchHit } from '@/api/types'
import { StatusBadge } from '@/components/common/StatusBadge'
import { formatDate } from '@/lib/dates'
import { knowledgeKindLabels, similarityLabel } from '@/lib/labels'

export function SearchHitLink({ hit, newTab }: { hit: SearchHit; newTab?: boolean }) {
  const to = hit.kind === 'Request' ? `/wnioski/${hit.id}` : `/baza-wiedzy?dokument=${hit.id}`
  return (
    <Link asChild fontWeight="semibold" color="brand.fg" textDecoration="underline" lineClamp={2}>
      <RouterLink to={to} target={newTab ? '_blank' : undefined} rel={newTab ? 'noopener' : undefined}>
        {hit.title}
        {newTab && (
          <>
            <ExternalLink size={13} aria-hidden style={{ display: 'inline', marginLeft: 4, verticalAlign: 'baseline' }} />
            <Box as="span" srOnly>
              {' '}
              (otwiera się w nowej karcie)
            </Box>
          </>
        )}
      </RouterLink>
    </Link>
  )
}

export function SimilarCasesList({ hits, newTab }: { hits: SearchHit[]; newTab?: boolean }) {
  return (
    <Stack as="ul" gap="0" listStyleType="none" m="0" p="0">
      {hits.map((hit) => (
        <Box as="li" key={`${hit.kind}-${hit.id}`} py="3" borderTopWidth="1px" borderColor="border.muted" _first={{ borderTopWidth: 0, pt: 0 }}>
          <Flex gap="2" fontSize="xs" color="fg.muted" mb="1" wrap="wrap" align="center">
            {hit.kind === 'Request' ? (
              <Text fontWeight="semibold">{hit.caseNumber}</Text>
            ) : (
              <HStack gap="1">
                <BookOpen size={12} aria-hidden />
                <Text fontWeight="semibold">{hit.knowledgeKind ? knowledgeKindLabels[hit.knowledgeKind] : 'Baza wiedzy'}</Text>
              </HStack>
            )}
            {hit.date && <Text>· {formatDate(hit.date)}</Text>}
          </Flex>
          <SearchHitLink hit={hit} newTab={newTab} />
          <Flex gap="2" mt="2" align="center" wrap="wrap">
            {hit.status && <StatusBadge status={hit.status} short />}
            {hit.departmentSymbols.map((symbol) => (
              <Tag.Root key={symbol} size="sm" variant="outline">
                <Tag.Label>{symbol}</Tag.Label>
              </Tag.Root>
            ))}
          </Flex>
          <Flex align="center" gap="2" mt="2">
            <Progress.Root value={Math.round(hit.score * 100)} size="xs" flex="1" maxW="32" colorPalette="brand" aria-hidden>
              <Progress.Track>
                <Progress.Range />
              </Progress.Track>
            </Progress.Root>
            <Text fontSize="xs" color="fg.muted">
              Podobieństwo: {similarityLabel(hit.score)}
            </Text>
          </Flex>
        </Box>
      ))}
    </Stack>
  )
}
