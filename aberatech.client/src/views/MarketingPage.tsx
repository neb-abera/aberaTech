import CssBaseline from "@mui/material/CssBaseline";
import Divider from "@mui/material/Divider";
import AppAppBar from "../components/AppAppBar";
import Features from "../components/Features";
import Footer from "../components/Footer";
import Hero from "../components/Hero";
import Highlights from "../components/Highlights";
import LogoCollection from "../components/LogoCollection";
import Pricing from "../components/Pricing";
import Testimonials from "../components/Testimonials";
import AppTheme from "../theme/AppTheme";

export default function MarketingPage(props: { disableCustomTheme?: boolean }) {
  return (
    <AppTheme {...props}>
      <CssBaseline enableColorScheme />

      <AppAppBar />
      <Hero />
      <div>
        <LogoCollection />
        <Features />
        <Divider />
        <Testimonials />
        <Divider />
        <Highlights />
        <Divider />
        <Pricing />
        <Divider />
        <Footer />
      </div>
    </AppTheme>
  );
}
