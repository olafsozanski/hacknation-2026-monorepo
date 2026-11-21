import { Badge, Box, Checkbox, Flex, Stack, Text } from '@chakra-ui/react'
import { History, Sparkles, Tags } from 'lucide-react'
import type { DepartmentSuggestion, SuggestionSource } from '@/api/types'
import { plural } from '@/lib/dates'
import { suggestionSourceLabels } from '@/lib/labels'

const sourceIcon: Record<SuggestionSource, typeof Sparkles> = { Ai: Sparkles, History: History, Keywords: Tags }

function historyHint(suggestion: DepartmentSuggestion) {
  const parts: string[] = []
  if (suggestion.similarCaseCount > 0)
    parts.push(`${suggestion.similarCaseCount} ${plural(suggestion.similarCaseCount, 'podobna sprawa', 'podobne sprawy', 'podobnych spraw')}`)
  if (suggestion.averageResponseDays !== null) parts.push(`odpowiedź średnio w ${Math.round(suggestion.averageResponseDays)} dni`)
  return parts.join(' · ')
}

type Props = {
  suggestions: DepartmentSuggestion[]
  selected: string[]
  onToggle: (departmentId: string, checked: boolean) => void
}

export function DepartmentSuggestionList({ suggestions, selected, onToggle }: Props) {
  return (
    <Stack gap="2" as="ul" listStyleType="none" m="0" p="0">
      {suggestions.map((suggestion) => {
        const Icon = sourceIcon[suggestion.source]
        const hint = historyHint(suggestion)
        const checked = selected.includes(suggestion.departmentId)
        return (
          <Box
            as="li"
            key={suggestion.departmentId}
            borderWidth="1px"
            borderColor={checked ? 'brand.solid' : 'border'}
            bg={checked ? 'brand.subtle' : 'bg.panel'}
            borderRadius="md"
            p="3"
          >
            <Checkbox.Root
              checked={checked}
              onCheckedChange={(e) => onToggle(suggestion.departmentId, e.checked === true)}
              alignItems="flex-start"
              w="full"
            >
              <Checkbox.HiddenInput />
              <Checkbox.Control mt="0.5" />
              <Box flex="1" minW="0">
                <Checkbox.Label>
                  <Text as="span" fontWeight="bold">
                    {suggestion.symbol}
                  </Text>{' '}
                  <Text as="span" fontWeight="semibold">
                    {suggestion.name}
                  </Text>
                </Checkbox.Label>
                <Text fontSize="sm" color="fg.muted" mt="1">
                  {suggestion.reason}
                </Text>
                <Flex gap="2" mt="2" wrap="wrap" align="center">
                  <Badge variant="outline" size="sm" gap="1">
                    <Icon size={12} aria-hidden />
                    {suggestionSourceLabels[suggestion.source]}
                  </Badge>
                  {hint && (
                    <Text fontSize="xs" color="fg.muted">
                      {hint}
                    </Text>
                  )}
                </Flex>
              </Box>
            </Checkbox.Root>
          </Box>
        )
      })}
    </Stack>
  )
}
