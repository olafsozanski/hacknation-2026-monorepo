import { Button, Text } from '@chakra-ui/react'
import { Link as RouterLink } from 'react-router'
import { PageHeader } from './PageHeader'

export function NotFoundPage() {
  return (
    <>
      <PageHeader title="Nie znaleziono strony" />
      <Text color="fg.muted" mb="6">
        Adres jest nieprawidłowy albo strona została przeniesiona.
      </Text>
      <Button asChild>
        <RouterLink to="/">Wróć do rejestru wniosków</RouterLink>
      </Button>
    </>
  )
}
