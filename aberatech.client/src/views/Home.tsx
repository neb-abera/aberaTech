import Hero from "../components/Hero";
import SiteFrame from "../components/SiteFrame";

export default function Home(props: { disableCustomTheme?: boolean }) {
  return (
    <SiteFrame {...props}>
      <Hero />
    </SiteFrame>
  );
}
