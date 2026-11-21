import { ChakraProvider } from '@chakra-ui/react'
import { QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { ToasterRegion } from '@/components/ui/ToasterRegion'
import { system } from '@/theme'
import { AccessibilityProvider } from './AccessibilityProvider'
import { queryClient } from './queryClient'

export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <ChakraProvider value={system}>
      <QueryClientProvider client={queryClient}>
        <AccessibilityProvider>
          {children}
          <ToasterRegion />
        </AccessibilityProvider>
      </QueryClientProvider>
    </ChakraProvider>
  )
}
