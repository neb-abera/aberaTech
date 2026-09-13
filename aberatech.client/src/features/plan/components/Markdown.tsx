import Box from "@mui/material/Box";
import Divider from "@mui/material/Divider";
import Link from "@mui/material/Link";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { type Block, type Inline, parseMarkdown } from "../core/markdown";

function renderInline(nodes: Inline[]): React.ReactNode[] {
  return nodes.map((node, index) => {
    const key = `${node.kind}-${index}`;
    switch (node.kind) {
      case "text":
        return <React.Fragment key={key}>{node.text}</React.Fragment>;
      case "bold":
        return <strong key={key}>{renderInline(node.children)}</strong>;
      case "italic":
        return <em key={key}>{renderInline(node.children)}</em>;
      case "code":
        return (
          <Box
            key={key}
            component="code"
            sx={{
              fontFamily: "monospace",
              fontSize: "0.9em",
              px: 0.5,
              borderRadius: 0.5,
              bgcolor: "action.hover",
            }}
          >
            {node.text}
          </Box>
        );
      case "link":
        return (
          <Link
            key={key}
            href={node.href}
            target="_blank"
            rel="noopener noreferrer"
          >
            {renderInline(node.children)}
          </Link>
        );
      default:
        return null;
    }
  });
}

const HEADING_VARIANTS = { 1: "h4", 2: "h5", 3: "h6", 4: "subtitle1" } as const;

function renderBlock(block: Block, index: number): React.ReactNode {
  const key = `${block.kind}-${index}`;
  switch (block.kind) {
    case "heading":
      return (
        <Typography
          key={key}
          variant={HEADING_VARIANTS[block.level]}
          component={`h${block.level + 1}` as "h2" | "h3" | "h4" | "h5"}
          sx={{ mt: block.level === 1 ? 0 : 3, mb: 1, fontWeight: 600 }}
        >
          {renderInline(block.children)}
        </Typography>
      );
    case "paragraph":
      return (
        <Typography key={key} variant="body1" sx={{ mb: 1.5 }}>
          {renderInline(block.children)}
        </Typography>
      );
    case "list":
      return (
        <Box
          key={key}
          component={block.ordered ? "ol" : "ul"}
          sx={{ pl: 3, mt: 0, mb: 1.5, "& li": { mb: 0.5 } }}
        >
          {block.items.map((item, i) => (
            // Items have no id of their own; their order is their identity.
            // biome-ignore lint/suspicious/noArrayIndexKey: position is the key
            <Typography key={i} component="li" variant="body1">
              {renderInline(item)}
            </Typography>
          ))}
        </Box>
      );
    case "table":
      return (
        <TableContainer key={key} sx={{ mb: 2 }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                {block.header.map((cell, i) => (
                  // biome-ignore lint/suspicious/noArrayIndexKey: position is the key
                  <TableCell key={i} sx={{ fontWeight: 600 }}>
                    {renderInline(cell)}
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {block.rows.map((row, r) => (
                // biome-ignore lint/suspicious/noArrayIndexKey: position is the key
                <TableRow key={r}>
                  {row.map((cell, c) => (
                    // biome-ignore lint/suspicious/noArrayIndexKey: position is the key
                    <TableCell key={c}>{renderInline(cell)}</TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      );
    case "code":
      return (
        <Box
          key={key}
          component="pre"
          sx={{
            fontFamily: "monospace",
            fontSize: "0.85rem",
            p: 1.5,
            borderRadius: 1,
            bgcolor: "action.hover",
            overflowX: "auto",
            mb: 1.5,
          }}
        >
          {block.text}
        </Box>
      );
    case "quote":
      return (
        <Typography
          key={key}
          variant="body1"
          component="blockquote"
          sx={{
            borderLeft: 3,
            borderColor: "divider",
            pl: 2,
            ml: 0,
            mb: 1.5,
            color: "text.secondary",
          }}
        >
          {renderInline(block.children)}
        </Typography>
      );
    case "rule":
      return <Divider key={key} sx={{ my: 3 }} />;
  }
}

/** The document, rendered. Parsed once per change of the source. */
export default function Markdown({ source }: { source: string }) {
  const blocks = React.useMemo(() => parseMarkdown(source), [source]);
  return <Box sx={{ maxWidth: 760 }}>{blocks.map(renderBlock)}</Box>;
}
