import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import Link from "@mui/material/Link";
import Typography from "@mui/material/Typography";
import * as React from "react";
import { Link as RouterLink, useSearchParams } from "react-router";
import SignInToSee from "../../progress/components/SignInToSee";
import {
  fetchPage,
  fetchTree,
  findPage,
  type PageResult,
  type PlaybookNode,
  type TreeResult,
} from "../core/api";
import Blocks from "./Blocks";

const busy = "Notion is busy. Reload in a minute.";
const failed = "Notion did not answer. Reload to try again.";

/**
 * The owner's Notion playbook, read live: the page tree on one side, the
 * open page on the other, and a download link for each file. Read only.
 * The open page is in the address, so a reload or Back keeps it.
 */
export default function PlaybookPanel() {
  const [tree, setTree] = React.useState<TreeResult | { status: "loading" }>({
    status: "loading",
  });
  const [page, setPage] = React.useState<PageResult | { status: "loading" }>({
    status: "loading",
  });
  const [params] = useSearchParams();

  React.useEffect(() => {
    let alive = true;
    void fetchTree().then((next) => {
      if (alive) setTree(next);
    });
    return () => {
      alive = false;
    };
  }, []);

  const root = tree.status === "owner" ? tree.root : null;
  const openId = params.get("page") ?? root?.id ?? null;

  React.useEffect(() => {
    if (!openId || !root) return;
    const controller = new AbortController();
    setPage({ status: "loading" });
    void fetchPage(openId, controller.signal).then((next) => {
      if (!controller.signal.aborted) setPage(next);
    });
    return () => controller.abort();
  }, [openId, root]);

  if (tree.status === "loading") {
    return <CircularProgress size={28} aria-label="Loading" />;
  }

  if (tree.status === "visitor") {
    return (
      <SignInToSee
        message="This page is the owner's. Sign in to see it."
        returnUrl="/playbook"
      />
    );
  }

  if (tree.status === "unconfigured") {
    return (
      <Alert severity="info">
        Notion is not connected. Set Notion__Token and Notion__PlaybookPageId on
        the container app.
      </Alert>
    );
  }

  if (tree.status === "error") {
    return <Alert severity="error">{tree.busy ? busy : failed}</Alert>;
  }

  const open = openId ? findPage(tree.root, openId) : undefined;

  return (
    <Box
      sx={{
        display: "grid",
        gap: 4,
        gridTemplateColumns: { xs: "1fr", md: "260px minmax(0, 1fr)" },
        alignItems: "start",
      }}
    >
      <Box component="nav" aria-label="Playbook pages">
        <Tree nodes={[tree.root]} openId={open?.id ?? null} />
      </Box>
      <Box component="article" aria-label="Page" sx={{ minWidth: 0 }}>
        <PageView page={page} />
      </Box>
    </Box>
  );
}

function Tree({
  nodes,
  openId,
}: {
  nodes: PlaybookNode[];
  openId: string | null;
}) {
  return (
    <Box component="ul" sx={{ listStyle: "none", pl: 0, my: 0 }}>
      {nodes.map((node) => (
        <Box component="li" key={node.id} sx={{ my: 0.5 }}>
          {node.kind === "page" ? (
            <Link
              component={RouterLink}
              to={`/playbook?page=${node.id}`}
              aria-current={node.id === openId ? "page" : undefined}
              underline={node.id === openId ? "always" : "hover"}
              sx={{ fontWeight: node.id === openId ? 600 : 400 }}
            >
              {node.title}
            </Link>
          ) : (
            <Typography
              component="span"
              variant="body2"
              sx={{ color: "text.secondary" }}
            >
              {node.title}
            </Typography>
          )}
          {node.children.length > 0 && (
            <Box sx={{ pl: 2 }}>
              <Tree nodes={node.children} openId={openId} />
            </Box>
          )}
        </Box>
      ))}
    </Box>
  );
}

function PageView({ page }: { page: PageResult | { status: "loading" } }) {
  switch (page.status) {
    case "loading":
      return <CircularProgress size={24} aria-label="Loading page" />;
    case "visitor":
      return (
        <Alert severity="warning">
          The session expired. Reload and sign in again.
        </Alert>
      );
    case "missing":
      return <Alert severity="info">That page is not in the playbook.</Alert>;
    case "error":
      return <Alert severity="error">{page.busy ? busy : failed}</Alert>;
    case "owner":
      return (
        <>
          <Typography variant="h3" component="h2" sx={{ mb: 2 }}>
            {page.page.title}
          </Typography>
          {page.page.blocks.length === 0 ? (
            <Typography sx={{ color: "text.secondary" }}>
              This page is empty.
            </Typography>
          ) : (
            <Blocks blocks={page.page.blocks} />
          )}
        </>
      );
  }
}
