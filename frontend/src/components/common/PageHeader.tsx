import { Box, Breadcrumb, Flex, Heading, Text } from '@chakra-ui/react'
import { ChevronRight } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link as RouterLink } from 'react-router'
import { usePageTitle } from './usePageTitle'

type Crumb = { label: string; to?: string }

type PageHeaderProps = {
  title: string
  eyebrow?: ReactNode
  description?: ReactNode
  actions?: ReactNode
  breadcrumbs?: Crumb[]
  badges?: ReactNode
  documentTitle?: string
}

export function PageHeader({ title, eyebrow, description, actions, breadcrumbs, badges, documentTitle }: PageHeaderProps) {
  usePageTitle(documentTitle ?? title)
  return (
    <Box as="header" mb={{ base: 5, md: 6 }}>
      {breadcrumbs && (
        <Breadcrumb.Root mb="3" size="sm" aria-label="Ścieżka nawigacji">
          <Breadcrumb.List>
            {breadcrumbs.map((crumb, index) => (
              <Breadcrumb.Item key={crumb.label}>
                {crumb.to ? (
                  <Breadcrumb.Link asChild color="brand.fg" textDecoration="underline">
                    <RouterLink to={crumb.to}>{crumb.label}</RouterLink>
                  </Breadcrumb.Link>
                ) : (
                  <Breadcrumb.CurrentLink>{crumb.label}</Breadcrumb.CurrentLink>
                )}
                {index < breadcrumbs.length - 1 && (
                  <Breadcrumb.Separator>
                    <ChevronRight size={14} aria-hidden />
                  </Breadcrumb.Separator>
                )}
              </Breadcrumb.Item>
            ))}
          </Breadcrumb.List>
        </Breadcrumb.Root>
      )}
      <Flex gap="4" align={{ base: 'stretch', md: 'flex-start' }} direction={{ base: 'column', md: 'row' }}>
        <Box flex="1" minW="0">
          {eyebrow && (
            <Text fontSize="sm" color="fg.muted" fontWeight="semibold" mb="1">
              {eyebrow}
            </Text>
          )}
          <Heading as="h1" size={{ base: '2xl', md: '3xl' }} tabIndex={-1} outline="none" lineHeight="1.2">
            {title}
          </Heading>
          {badges && (
            <Flex gap="2" mt="3" wrap="wrap" align="center">
              {badges}
            </Flex>
          )}
          {description && (
            <Text mt="2" color="fg.muted" maxW="3xl">
              {description}
            </Text>
          )}
        </Box>
        {actions && (
          <Flex gap="2" wrap="wrap" align="center" justify={{ base: 'flex-start', md: 'flex-end' }}>
            {actions}
          </Flex>
        )}
      </Flex>
    </Box>
  )
}
