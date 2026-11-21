import { Code, Link, Stack, Text, chakra } from '@chakra-ui/react'
import { Fragment, useMemo } from 'react'
import { Link as RouterLink } from 'react-router'
import { parseMarkdown, type Block, type Inline } from '../markdown'

function Inlines({ nodes }: { nodes: Inline[] }) {
  return (
    <>
      {nodes.map((node, index) => {
        switch (node.kind) {
          case 'text':
            return <Fragment key={index}>{node.text}</Fragment>
          case 'strong':
            return (
              <strong key={index}>
                <Inlines nodes={node.children} />
              </strong>
            )
          case 'code':
            return (
              <Code key={index} size="sm" variant="subtle">
                {node.text}
              </Code>
            )
          case 'link':
            return (
              <Link key={index} asChild color="brand.fg" textDecoration="underline" fontWeight="semibold">
                <RouterLink to={node.href}>{node.text}</RouterLink>
              </Link>
            )
        }
      })}
    </>
  )
}

function MarkdownBlock({ block }: { block: Block }) {
  if (block.kind === 'paragraph') {
    return (
      <Text>
        {block.lines.map((line, index) => (
          <Fragment key={index}>
            {index > 0 && <br />}
            <Inlines nodes={line} />
          </Fragment>
        ))}
      </Text>
    )
  }
  const items = block.items.map((item, index) => (
    <chakra.li key={index} ps="1" _notFirst={{ mt: 1 }}>
      <Inlines nodes={item} />
    </chakra.li>
  ))
  return block.ordered ? (
    <chakra.ol start={block.start} ps="6" listStyleType="decimal">
      {items}
    </chakra.ol>
  ) : (
    <chakra.ul ps="6" listStyleType="disc">
      {items}
    </chakra.ul>
  )
}

export function AssistantMarkdown({ content }: { content: string }) {
  const blocks = useMemo(() => parseMarkdown(content), [content])
  return (
    <Stack gap="2" overflowWrap="anywhere">
      {blocks.map((block, index) => (
        <MarkdownBlock key={index} block={block} />
      ))}
    </Stack>
  )
}
