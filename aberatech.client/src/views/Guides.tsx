import SectionIndex from '../components/SectionIndex';
import { guides } from '../site/sections';

export default function Guides(props: { disableCustomTheme?: boolean }) {
  return <SectionIndex {...props} title="Guides" entries={guides} />;
}
