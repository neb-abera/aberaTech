import Box from "@mui/material/Box";
import Divider from "@mui/material/Divider";
import Link from "@mui/material/Link";
import Typography from "@mui/material/Typography";
import type * as React from "react";
import { Link as RouterLink } from "react-router";
import { fileUrl, type PlaybookBlock, type PlaybookText } from "../core/api";

/** A run of text with its marks. A link to a playbook page stays on this site. */
export function Rich({ runs }: { runs?: PlaybookText[] }) {
  if (!runs?.length) return null;
  return (
    <>
      {runs.map((run, index) => {
        let node: React.ReactNode = run.text;
        if (run.code) node = <code>{node}</code>;
        if (run.bold) node = <strong>{node}</strong>;
        if (run.italic) node = <em>{node}</em>;
        if (run.strikethrough) node = <s>{node}</s>;
        if (run.underline) node = <u>{node}</u>;
        if (run.href?.startsWith("/playbook"))
          node = (
            <Link component={RouterLink} to={run.href}>
              {node}
            </Link>
          );
        else if (run.href)
          node = (
            <Link href={run.href} target="_blank" rel="noopener noreferrer">
              {node}
            </Link>
          );
        // Runs never move or change once a page is drawn.
        // biome-ignore lint/suspicious/noArrayIndexKey: the index is the identity
        return <span key={index}>{node}</span>;
      })}
    </>
  );
}

const listTags: Record<string, "ul" | "ol"> = {
  bulleted: "ul",
  numbered: "ol",
  todo: "ul",
};

/**
 * A page's blocks. Consecutive list items of one kind share one list, as
 * Notion draws them.
 */
export default function Blocks({ blocks }: { blocks: PlaybookBlock[] }) {
  const groups: { tag?: "ul" | "ol"; type: string; items: PlaybookBlock[] }[] =
    [];
  for (const block of blocks) {
    const tag = listTags[block.type];
    const last = groups[groups.length - 1];
    if (tag && last?.type === block.type) last.items.push(block);
    else groups.push({ tag, type: block.type, items: [block] });
  }

  return (
    <>
      {groups.map((group, index) =>
        group.tag ? (
          <Box
            // biome-ignore lint/suspicious/noArrayIndexKey: blocks do not reorder
            key={index}
            component={group.tag}
            sx={{
              pl: 3,
              my: 1,
              ...(group.type === "todo" ? { listStyle: "none", pl: 1 } : {}),
            }}
          >
            {group.items.map((item, i) => (
              // biome-ignore lint/suspicious/noArrayIndexKey: blocks do not reorder
              <li key={i}>
                {group.type === "todo" && (
                  <input
                    type="checkbox"
                    checked={item.checked === true}
                    readOnly
                    disabled
                    aria-label={(item.text ?? [])
                      .map((run) => run.text)
                      .join("")}
                    style={{ marginRight: 8 }}
                  />
                )}
                <Rich runs={item.text} />
                {item.children && <Blocks blocks={item.children} />}
              </li>
            ))}
          </Box>
        ) : (
          // biome-ignore lint/suspicious/noArrayIndexKey: blocks do not reorder
          <Block key={index} block={group.items[0]} />
        ),
      )}
    </>
  );
}

function Children({ block }: { block: PlaybookBlock }) {
  return block.children ? (
    <Box sx={{ pl: 3 }}>
      <Blocks blocks={block.children} />
    </Box>
  ) : null;
}

function Block({ block }: { block: PlaybookBlock }) {
  switch (block.type) {
    case "heading": {
      const level = Math.min(3, Math.max(1, block.level ?? 1));
      const variant = (["h4", "h5", "h6"] as const)[level - 1];
      const component = (["h2", "h3", "h4"] as const)[level - 1];
      return (
        <>
          <Typography variant={variant} component={component} sx={{ mt: 3 }}>
            <Rich runs={block.text} />
          </Typography>
          <Children block={block} />
        </>
      );
    }
    case "paragraph":
    case "equation":
      return (
        <>
          <Typography variant="body1" sx={{ my: 1, minHeight: "1em" }}>
            <Rich runs={block.text} />
          </Typography>
          <Children block={block} />
        </>
      );
    case "toggle":
      return (
        <Box component="details" sx={{ my: 1 }}>
          <summary>
            <Rich runs={block.text} />
          </summary>
          <Children block={block} />
        </Box>
      );
    case "quote":
      return (
        <Box
          component="blockquote"
          sx={{ borderLeft: 3, borderColor: "divider", pl: 2, mx: 0, my: 1 }}
        >
          <Rich runs={block.text} />
          <Children block={block} />
        </Box>
      );
    case "callout":
      return (
        <Box
          role="note"
          sx={{
            display: "flex",
            gap: 1.5,
            p: 2,
            my: 1,
            borderRadius: 1,
            bgcolor: "action.hover",
          }}
        >
          {block.icon && <span aria-hidden="true">{block.icon}</span>}
          <Box>
            <Rich runs={block.text} />
            {block.children && <Blocks blocks={block.children} />}
          </Box>
        </Box>
      );
    case "code":
      return (
        <Box
          component="pre"
          sx={{
            p: 2,
            my: 1,
            overflowX: "auto",
            borderRadius: 1,
            bgcolor: "action.hover",
          }}
        >
          <code data-language={block.language}>
            {(block.text ?? []).map((run) => run.text).join("")}
          </code>
        </Box>
      );
    case "divider":
      return <Divider sx={{ my: 2 }} />;
    case "table":
      return <Table block={block} />;
    case "page":
      return (
        <Typography variant="body1" sx={{ my: 1 }}>
          {block.id ? (
            <Link component={RouterLink} to={`/playbook?page=${block.id}`}>
              {block.title}
            </Link>
          ) : (
            block.title
          )}
        </Typography>
      );
    case "database":
      return (
        <Box sx={{ my: 2 }}>
          <Typography variant="subtitle1" component="p">
            {block.title}
          </Typography>
          <Box component="ul" sx={{ pl: 3, my: 0.5 }}>
            {(block.pages ?? []).map((page) => (
              <li key={page.id}>
                <Link component={RouterLink} to={`/playbook?page=${page.id}`}>
                  {page.title}
                </Link>
              </li>
            ))}
          </Box>
        </Box>
      );
    case "file":
      return (
        <Box sx={{ my: 1 }}>
          {block.url ? (
            <Link href={block.url} target="_blank" rel="noopener noreferrer">
              {block.name}
            </Link>
          ) : (
            <Link href={fileUrl(block.id ?? "")} download={block.name}>
              Download {block.name}
            </Link>
          )}
          {block.caption && block.caption.length > 0 && (
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              <Rich runs={block.caption} />
            </Typography>
          )}
        </Box>
      );
    case "link":
      return (
        <Box sx={{ my: 1 }}>
          <Link href={block.url} target="_blank" rel="noopener noreferrer">
            {block.caption?.length ? <Rich runs={block.caption} /> : block.url}
          </Link>
        </Box>
      );
    default:
      return (
        <Typography variant="body2" sx={{ my: 1, color: "text.secondary" }}>
          A {block.notionType ?? block.type} block. Open it in Notion.
        </Typography>
      );
  }
}

function Table({ block }: { block: PlaybookBlock }) {
  const rows = block.rows ?? [];
  return (
    <Box sx={{ overflowX: "auto", my: 2 }}>
      <Box
        component="table"
        sx={{
          borderCollapse: "collapse",
          "& td, & th": {
            border: 1,
            borderColor: "divider",
            px: 1,
            py: 0.5,
            textAlign: "left",
            verticalAlign: "top",
          },
        }}
      >
        <tbody>
          {rows.map((row, r) => (
            // biome-ignore lint/suspicious/noArrayIndexKey: rows do not reorder
            <tr key={r}>
              {row
                .map((cell, c) => ({ cell, c, key: `${r}:${c}` }))
                .map(({ cell, c, key }) => {
                  const header =
                    (block.headerRow && r === 0) ||
                    (block.headerColumn && c === 0);
                  const Cell = header ? "th" : "td";
                  return (
                    <Cell
                      key={key}
                      scope={header ? (r === 0 ? "col" : "row") : undefined}
                    >
                      <Rich runs={cell} />
                    </Cell>
                  );
                })}
            </tr>
          ))}
        </tbody>
      </Box>
    </Box>
  );
}
