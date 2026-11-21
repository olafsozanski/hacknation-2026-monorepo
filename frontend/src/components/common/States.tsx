import { Alert, Box, Button, EmptyState, Spinner, Stack, Text, VStack } from '@chakra-ui/react'
import { Inbox, RotateCw } from 'lucide-react'
import type { ReactNode } from 'react'

export function LoadingState({ label = 'Wczytywanie…' }: { label?: string }) {
  return (
    <Box py="12" display="flex" justifyContent="center" role="status" aria-live="polite">
      <Stack direction="row" align="center" gap="3" color="fg.muted">
        <Spinner size="sm" color="brand.solid" />
        <Text>{label}</Text>
      </Stack>
    </Box>
  )
}

export function ErrorState({ error, onRetry, title = 'Nie udało się wczytać danych' }: {
  error: unknown
  onRetry?: () => void
  title?: string
}) {
  return (
    <Alert.Root status="error" variant="outline" borderRadius="md">
      <Alert.Indicator />
      <Alert.Content>
        <Alert.Title>{title}</Alert.Title>
        <Alert.Description>{error instanceof Error ? error.message : 'Spróbuj ponownie za chwilę.'}</Alert.Description>
      </Alert.Content>
      {onRetry && (
        <Button size="sm" variant="outline" colorPalette="red" onClick={onRetry} alignSelf="center">
          <RotateCw size={16} aria-hidden /> Spróbuj ponownie
        </Button>
      )}
    </Alert.Root>
  )
}

export function EmptyHint({ title, description, icon, action }: {
  title: string
  description?: ReactNode
  icon?: ReactNode
  action?: ReactNode
}) {
  return (
    <EmptyState.Root size="sm">
      <EmptyState.Content>
        <EmptyState.Indicator color="fg.subtle">{icon ?? <Inbox aria-hidden />}</EmptyState.Indicator>
        <VStack textAlign="center" gap="1">
          <EmptyState.Title>{title}</EmptyState.Title>
          {description && <EmptyState.Description color="fg.muted">{description}</EmptyState.Description>}
        </VStack>
        {action}
      </EmptyState.Content>
    </EmptyState.Root>
  )
}
