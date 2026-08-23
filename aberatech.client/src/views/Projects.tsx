import SectionIndex from '../components/SectionIndex';
import { projects } from '../site/sections';

export default function Projects(props: { disableCustomTheme?: boolean }) {
  return (
    <SectionIndex
      {...props}
      title="Projects"
      intro="Things that do something, rather than things to read."
      entries={projects}
    />
  );
}
