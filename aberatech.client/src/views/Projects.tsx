import SectionIndex from '../components/SectionIndex';
import { projects } from '../site/sections';

export default function Projects(props: { disableCustomTheme?: boolean }) {
  return <SectionIndex {...props} title="Projects" entries={projects} />;
}
