import { Box, Button, Heading, Text } from '@chakra-ui/react'
import { isRouteErrorResponse, Link as RouterLink, useRouteError } from 'react-router'
import { ApiError } from '@/api/client'

export function RouteError() {
  const error = useRouteError()
  const notFound = (isRouteErrorResponse(error) && error.status === 404) || (error instanceof ApiError && error.status === 404)
  return (
    <Box maxW="2xl" mx="auto" py="16" px="6" role="alert">
      <Heading as="h1" size="2xl" mb="3">
        {notFound ? 'Nie znaleziono strony' : 'Coś poszło nie tak'}
      </Heading>
      <Text color="fg.muted" mb="6">
        {notFound
          ? 'Sprawa lub strona, której szukasz, nie istnieje albo została usunięta.'
          : 'Wystąpił nieoczekiwany błąd. Odśwież stronę. Jeśli problem się powtarza, zgłoś go do Wydziału Informatyki.'}
      </Text>
      <Button asChild>
        <RouterLink to="/">Wróć do rejestru wniosków</RouterLink>
      </Button>
    </Box>
  )
}
