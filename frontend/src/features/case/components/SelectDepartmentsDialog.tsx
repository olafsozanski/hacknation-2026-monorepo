import { Box, Button, Checkbox, CloseButton, Dialog, Input, InputGroup, Portal, Stack, Text } from '@chakra-ui/react'
import { Search } from 'lucide-react'
import { useMemo, useState } from 'react'
import type { RequestDetail } from '@/api/types'
import { notifyError } from '@/components/ui/toaster'
import { useDepartments } from '@/features/platform/api'
import { useSetDepartments } from '../api'

export function SelectDepartmentsDialog({ request, open, onClose }: { request: RequestDetail; open: boolean; onClose: () => void }) {
  const { data: departments = [] } = useDepartments()
  const save = useSetDepartments(request.id)
  const [selected, setSelected] = useState(() => request.departments.map((d) => d.departmentId))
  const [filter, setFilter] = useState('')

  const visible = useMemo(() => {
    const needle = filter.trim().toLowerCase()
    return needle
      ? departments.filter((d) => `${d.symbol} ${d.name}`.toLowerCase().includes(needle))
      : departments
  }, [departments, filter])

  const toggle = (id: string, checked: boolean) =>
    setSelected((ids) => (checked ? [...ids, id] : ids.filter((x) => x !== id)))

  const submit = () =>
    save.mutate(selected, {
      onSuccess: onClose,
      onError: (error) => notifyError(error, 'Nie udało się zapisać wydziałów'),
    })

  return (
    <Dialog.Root open={open} onOpenChange={(e) => !e.open && onClose()} size="lg" scrollBehavior="inside">
      <Portal>
        <Dialog.Backdrop />
        <Dialog.Positioner>
          <Dialog.Content>
            <Dialog.Header>
              <Dialog.Title>Wydziały i biura w sprawie</Dialog.Title>
            </Dialog.Header>
            <Dialog.Body>
              <InputGroup startElement={<Search size={16} aria-hidden />} mb="3">
                <Input
                  type="search"
                  aria-label="Filtruj wydziały"
                  placeholder="Szukaj po nazwie lub symbolu"
                  value={filter}
                  onChange={(e) => setFilter(e.target.value)}
                />
              </InputGroup>
              <Stack gap="1" role="group" aria-label="Wydziały i biura">
                {visible.map((d) => (
                  <Box key={d.id} px="2" py="1.5" borderRadius="sm" _hover={{ bg: 'bg.muted' }}>
                    <Checkbox.Root checked={selected.includes(d.id)} onCheckedChange={(e) => toggle(d.id, e.checked === true)}>
                      <Checkbox.HiddenInput />
                      <Checkbox.Control />
                      <Checkbox.Label>
                        <Text as="span" fontWeight="bold" mr="2">
                          {d.symbol}
                        </Text>
                        {d.name}
                      </Checkbox.Label>
                    </Checkbox.Root>
                  </Box>
                ))}
              </Stack>
            </Dialog.Body>
            <Dialog.Footer>
              <Text fontSize="sm" color="fg.muted" mr="auto">
                Zaznaczono: {selected.length}
              </Text>
              <Button variant="outline" onClick={onClose}>
                Anuluj
              </Button>
              <Button onClick={submit} loading={save.isPending}>
                Zapisz
              </Button>
            </Dialog.Footer>
            <Dialog.CloseTrigger asChild>
              <CloseButton size="sm" aria-label="Zamknij" />
            </Dialog.CloseTrigger>
          </Dialog.Content>
        </Dialog.Positioner>
      </Portal>
    </Dialog.Root>
  )
}
