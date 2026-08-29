import * as React from 'react';
import {styled, alpha} from '@mui/material/styles';
import Box from '@mui/material/Box';
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Container from '@mui/material/Container';
import Divider from '@mui/material/Divider';
import MenuItem from '@mui/material/MenuItem';
import MenuList from '@mui/material/MenuList';
import Typography from '@mui/material/Typography';
import Drawer from '@mui/material/Drawer';
import MenuIcon from '@mui/icons-material/Menu';
import CloseRoundedIcon from '@mui/icons-material/CloseRounded';
import FingerprintIcon from '@mui/icons-material/Fingerprint';
import ColorModeIconDropdown from '../theme/ColorModeIconDropdown';
import {Link} from 'react-router';
import { guides, label, primaryAction, projects } from '../site/sections';

const StyledToolbar = styled(Toolbar)(({theme}) => ({
  display        : 'flex',
  alignItems     : 'center',
  justifyContent : 'space-between',
  flexShrink     : 0,
  borderRadius   : `calc(${theme.shape.borderRadius}px + 8px)`,
  backdropFilter : 'blur(24px)',
  border         : '1px solid',
  // Through the CSS-variable theme, so the glass tints to whichever scheme is
  // actually showing. The static palette here is the dark default, which made
  // the bar an opaque grey slab on the light background.
  borderColor    : (theme.vars || theme).palette.divider,
  backgroundColor: theme.vars
    ? `rgba(${theme.vars.palette.background.defaultChannel} / 0.4)`
    : alpha(theme.palette.background.default, 0.4),
  boxShadow      : theme.shadows[1],
  padding        : '8px 12px'
}));

export default function AppAppBar()
{
  const [open, setOpen] = React.useState(false);

  const toggleDrawer = (newOpen: boolean) => () =>
  {
    setOpen(newOpen);
  };

  return (
    <AppBar
      position="fixed"
      enableColorOnDark
      sx={{
        boxShadow      : 0,
        bgcolor        : 'transparent',
        backgroundImage: 'none',
        mt             : 'calc(var(--template-frame-height, 0px) + 28px)'
      }}
    >
      <Container maxWidth="lg">
        <StyledToolbar variant="dense" disableGutters>
          <Box sx={{flexGrow: 1, display: 'flex', alignItems: 'center', px: 0}}>
            {/* The app bar inherits primary.contrastText, which is near white
                in both schemes — invisible against a light background. The
                icon takes the theme's own text colour instead, so it follows
                whichever scheme is showing. */}
            <FingerprintIcon sx={{ color: 'text.primary' }} />
            {/* Three items, and it stays three however many projects there are.
                Everything else is reachable from /guides and /projects. */}
            <Box sx={{display: {xs: 'none', md: 'flex'}}}>
              <Button variant="text" color="info" size="small" component={Link} to="/">
                Home
              </Button>
              <Button variant="text" color="info" size="small" component={Link} to="/guides">
                Guides
              </Button>
              <Button variant="text" color="info" size="small" component={Link} to="/projects">
                Projects
              </Button>
            </Box>
          </Box>
          <Box
            sx={{
              display   : {xs: 'none', md: 'flex'},
              gap       : 1,
              alignItems: 'center'
            }}
          >
            {/* Booking is something to do rather than somewhere to browse, so it
                is a button beside the theme control and not a fourth tab. It is
                still listed on /projects, because it is also a project. */}
            <Button
              variant="contained"
              color="primary"
              size="small"
              component={Link}
              to={primaryAction.to}
              sx={{
                '&&': {
                  bgcolor: 'primary.main',
                backgroundImage: 'none',
                color: 'primary.contrastText',
                borderColor: 'primary.main',
                boxShadow: 'none',
                  '&:hover': { bgcolor: 'primary.dark', backgroundImage: 'none', boxShadow: 'none' }
                }
              }}
            >
              {primaryAction.title}
            </Button>
            <ColorModeIconDropdown/>
          </Box>
          <Box sx={{display: {xs: 'flex', md: 'none'}, gap: 1}}>
            <ColorModeIconDropdown size="medium"/>
            <IconButton aria-label="Menu button" onClick={toggleDrawer(true)}>
              <MenuIcon/>
            </IconButton>
            <Drawer
              anchor="top"
              open={open}
              onClose={toggleDrawer(false)}
              slotProps={{
                paper: {
                  sx: {
                    top: 'var(--template-frame-height, 0px)'
                  }
                }
              }}
            >
              <Box sx={{p: 2, backgroundColor: 'background.default'}}>
                <Box
                  sx={{
                    display       : 'flex',
                    justifyContent: 'flex-end'
                  }}
                >
                  <IconButton onClick={toggleDrawer(false)}>
                    <CloseRoundedIcon/>
                  </IconButton>
                </Box>
                {/* The action first, and as a button, because on a phone this
                    drawer is the whole navigation and booking is the thing most
                    likely to be wanted. */}
                <Button
                  variant="contained"
                  color="primary"
                  fullWidth
                  component={Link}
                  to={primaryAction.to}
                  sx={{
                    mb: 1.5,
                    '&&': {
                      bgcolor: 'primary.main',
                    backgroundImage: 'none',
                    color: 'primary.contrastText',
                    borderColor: 'primary.main',
                    boxShadow: 'none',
                      '&:hover': { bgcolor: 'primary.dark', backgroundImage: 'none', boxShadow: 'none' }
                    }
                  }}
                >
                  {primaryAction.title}
                </Button>
                {/* MUI 9 requires MenuItems to sit inside a Menu or MenuList;
                    a bare one throws, and a throw here unmounts the whole app. */}
                <MenuList sx={{p: 0}}>
                  <MenuItem component={Link} to="/">
                    Home
                  </MenuItem>
                  <Divider sx={{my: 1}}/>
                  <Typography variant="caption" sx={{color: 'text.disabled', px: 2, pt: 1, display: 'block'}}>
                    Guides
                  </Typography>
                  {guides.map((entry) => (
                    <MenuItem key={entry.to} component={Link} to={entry.to}>
                      {label(entry)}
                    </MenuItem>
                  ))}
                  <Divider sx={{my: 1}}/>
                  <Typography variant="caption" sx={{color: 'text.disabled', px: 2, pt: 1, display: 'block'}}>
                    Projects
                  </Typography>
                  {projects.map((entry) =>
                    entry.external ? (
                      <MenuItem
                        key={entry.to}
                        component="a"
                        href={entry.to}
                        target="_blank"
                        rel="noopener noreferrer"
                      >
                        {label(entry)}
                      </MenuItem>
                    ) : (
                      <MenuItem key={entry.to} component={Link} to={entry.to}>
                        {label(entry)}
                      </MenuItem>
                    )
                  )}
                </MenuList>
                <Divider sx={{my: 3}}/>
              </Box>
            </Drawer>
          </Box>
        </StyledToolbar>
      </Container>
    </AppBar>
  );
}
