import { Alert, Box, Button, Card, Field, Flex, Heading, Input, Stack, Text, chakra } from '@chakra-ui/react'
import { LockKeyhole } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { AppMark } from '@/components/common/AppMark'
import { FieldErrorText } from '@/components/common/FieldErrorText'
import { usePageTitle } from '@/components/common/usePageTitle'
import { A11yControls } from '@/components/layout/A11yControls'
import { collectErrors, focusFirstInvalid, hasErrors, useFieldErrors } from '@/lib/validation'
import { useLogin } from './session'

function safeReturnPath(value: string | null) {
  return value && value.startsWith('/') && !value.startsWith('//') ? value : '/'
}

export function LoginPage() {
  usePageTitle('Logowanie')
  const login = useLogin()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [errors, setErrors, clearErrors] = useFieldErrors()

  const submit = (event: FormEvent<HTMLElement>) => {
    event.preventDefault()
    const found = collectErrors({
      userName: !userName.trim() && 'Podaj nazwę użytkownika.',
      password: !password && 'Podaj hasło.',
    })
    setErrors(found)
    if (hasErrors(found)) {
      focusFirstInvalid(event.currentTarget)
      return
    }
    login.mutate(
      { userName, password },
      { onSuccess: () => navigate(safeReturnPath(params.get('wroc')), { replace: true }) },
    )
  }

  return (
    <Flex minH="100dvh" direction="column" bg="bg.canvas">
      <Flex justify="flex-end" p="3">
        <A11yControls />
      </Flex>
      <Flex as="main" flex="1" align="center" justify="center" px="4" pb="16">
        <Box w="full" maxW="md">
          <Flex align="center" gap="3" mb="6" justify="center">
            <AppMark boxSize="11" />
            <Box>
              <Text fontWeight="bold" fontSize="xl" lineHeight="1.1">
                Asystent UDIP
              </Text>
              <Text fontSize="sm" color="fg.muted">
                Obsługa wniosków o udostępnienie informacji publicznej
              </Text>
            </Box>
          </Flex>
          <Card.Root variant="outline" bg="bg.panel">
            <Card.Body p={{ base: 6, md: 8 }}>
              <chakra.form onSubmit={submit} noValidate>
              <Heading as="h1" size="xl" mb="1">
                Zaloguj się
              </Heading>
              <Text color="fg.muted" mb="6" fontSize="sm">
                Użyj konta nadanego przez administratora.
              </Text>
              <Stack gap="5">
                {login.error && (
                  <Alert.Root status="error" variant="outline" role="alert">
                    <Alert.Indicator />
                    <Alert.Title>{login.error.message}</Alert.Title>
                  </Alert.Root>
                )}
                <Field.Root required invalid={!!errors.userName}>
                  <Field.Label>Nazwa użytkownika</Field.Label>
                  <Input
                    name="username"
                    autoComplete="username"
                    autoFocus
                    size="lg"
                    value={userName}
                    onChange={(e) => {
                      setUserName(e.target.value)
                      clearErrors('userName')
                    }}
                  />
                  <FieldErrorText>{errors.userName}</FieldErrorText>
                </Field.Root>
                <Field.Root required invalid={!!errors.password}>
                  <Field.Label>Hasło</Field.Label>
                  <Input
                    name="password"
                    type="password"
                    autoComplete="current-password"
                    size="lg"
                    value={password}
                    onChange={(e) => {
                      setPassword(e.target.value)
                      clearErrors('password')
                    }}
                  />
                  <FieldErrorText>{errors.password}</FieldErrorText>
                </Field.Root>
                <Button type="submit" size="lg" loading={login.isPending}>
                  Zaloguj się
                </Button>
              </Stack>
              </chakra.form>
            </Card.Body>
          </Card.Root>
          <Flex gap="2" mt="5" color="fg.muted" fontSize="sm" align="flex-start">
            <Box pt="0.5">
              <LockKeyhole size={16} aria-hidden />
            </Box>
            <Text>
              Dostęp wyłącznie dla upoważnionych pracowników urzędu. Działania w systemie są rejestrowane. Po pięciu
              nieudanych próbach konto zostaje czasowo zablokowane.
            </Text>
          </Flex>
        </Box>
      </Flex>
    </Flex>
  )
}
