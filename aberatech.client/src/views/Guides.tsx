import SectionIndex from '../components/SectionIndex';
import { guides } from '../site/sections';

export default function Guides(props: { disableCustomTheme?: boolean }) {
  return (
    <SectionIndex
      {...props}
      title="Guides"
      intro="Long-form things I wrote because I needed them and could not find them."
      entries={guides}
    />
  );
}
