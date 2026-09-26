import CardMedia from "@mui/material/CardMedia";
import type { SxProps, Theme } from "@mui/material/styles";
import { useContext } from "react";
import { Link } from "react-router";
import { SectionOpen } from "./GuideSection";

/**
 * The page a reader opens to see an embedded document in its own tab: the
 * Google Docs viewer without its embedded chrome, or the YouTube watch page.
 */
export function documentLink(src: string): string {
  const video = src.match(/^https:\/\/www\.youtube\.com\/embed\/([\w-]+)$/);
  if (video) return `https://www.youtube.com/watch?v=${video[1]}`;
  return src.replace(/&embedded=true$/, "");
}

/**
 * An embedded document or video that loads only once its section is open.
 *
 * /transition carried 22 of these inside closed sections, and every one
 * loaded on every visit. While closed this is a link to the same document,
 * so the prerendered HTML still names it.
 */
export default function DocumentFrame(props: {
  title: string;
  src: string;
  sx?: SxProps<Theme>;
}) {
  const { title, src, sx } = props;
  const open = useContext(SectionOpen);

  if (!open) {
    return (
      <Link to={documentLink(src)} target="_blank" rel="noopener noreferrer">
        {title}
      </Link>
    );
  }
  return (
    <CardMedia
      component="iframe"
      title={title}
      src={src}
      loading="lazy"
      sx={sx}
    />
  );
}
