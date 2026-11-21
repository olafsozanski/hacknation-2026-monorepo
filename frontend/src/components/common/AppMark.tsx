import { Box, type BoxProps } from '@chakra-ui/react'

export function AppMark(props: BoxProps) {
  return (
    <Box as="span" display="inline-flex" flexShrink={0} aria-hidden {...props}>
      <svg viewBox="0 0 32 32" width="100%" height="100%" focusable="false">
        <rect width="32" height="32" rx="6" fill="var(--chakra-colors-brand-solid)" />
        <path d="M10 7h9l5 5v13a1 1 0 0 1-1 1H10a1 1 0 0 1-1-1V8a1 1 0 0 1 1-1Z" fill="var(--chakra-colors-brand-contrast)" />
        <path d="M19 7v5h5" fill="var(--chakra-colors-brand-emphasized)" />
        <path
          d="M12.5 16.5h7M12.5 19.5h7M12.5 22.5h4"
          stroke="var(--chakra-colors-brand-solid)"
          strokeWidth="1.6"
          strokeLinecap="round"
        />
      </svg>
    </Box>
  )
}
