import React from "react";

// chakra imports
import {
  Box,
  Flex,
  Drawer,
  DrawerBody,
  Icon,
  useColorModeValue,
  DrawerOverlay,
  useDisclosure,
  DrawerContent,
  DrawerCloseButton,
} from "@chakra-ui/react";
import Content from "@horizon/components/sidebar/components/Content";
import {
  renderThumb,
  renderTrack,
  renderView,
} from "@horizon/components/scrollbar/Scrollbar";
import { Scrollbars } from "react-custom-scrollbars-2";
import PropTypes from "prop-types";
import { SidebarContext } from "@horizon/contexts/SidebarContext";

// Assets
import { IoMenuOutline } from "react-icons/io5";

function Sidebar(props) {
  const { routes } = props;
  const sidebarContext = React.useContext(SidebarContext) || {};
  const isSidebarCollapsed = Boolean(sidebarContext.isSidebarCollapsed);
  const setIsSidebarCollapsed = sidebarContext.setIsSidebarCollapsed;
  const setSidebarWidth = sidebarContext.setSidebarWidth;
  const setIsSidebarDragging = sidebarContext.setIsSidebarDragging;
  const sidebarMinWidth = sidebarContext.sidebarMinWidth || 72;
  const sidebarMaxWidth = sidebarContext.sidebarMaxWidth || 300;
  const sidebarWidth = sidebarContext.sidebarWidth || 300;
  const [isDragging, setIsDragging] = React.useState(false);
  const dragStartXRef = React.useRef(0);
  const dragStartWidthRef = React.useRef(sidebarWidth);
  const currentWidthRef = React.useRef(sidebarWidth);
  const movedRef = React.useRef(false);
  const pendingWidthRef = React.useRef(sidebarWidth);
  const lastAppliedWidthRef = React.useRef(sidebarWidth);
  const rafRef = React.useRef(null);

  const applySidebarWidthCssVar = React.useCallback((width) => {
    if (typeof document === "undefined") return;
    document.documentElement.style.setProperty("--admin-sidebar-width", `${width}px`);
  }, []);

  React.useEffect(() => {
    currentWidthRef.current = sidebarWidth;
    lastAppliedWidthRef.current = sidebarWidth;
    pendingWidthRef.current = sidebarWidth;
    applySidebarWidthCssVar(sidebarWidth);
  }, [sidebarWidth, applySidebarWidthCssVar]);

  React.useEffect(() => {
    return () => {
      setIsSidebarDragging?.(false);
      if (rafRef.current !== null && typeof window !== "undefined") {
        window.cancelAnimationFrame(rafRef.current);
        rafRef.current = null;
      }
      if (typeof document !== "undefined") {
        document.body.style.userSelect = "";
        document.body.style.cursor = "";
      }
    };
  }, [setIsSidebarDragging]);

  React.useEffect(() => {
    if (!isDragging) return undefined;

    const flushPendingWidth = () => {
      if (rafRef.current !== null && typeof window !== "undefined") {
        window.cancelAnimationFrame(rafRef.current);
        rafRef.current = null;
      }
      if (pendingWidthRef.current !== lastAppliedWidthRef.current) {
        lastAppliedWidthRef.current = pendingWidthRef.current;
        currentWidthRef.current = pendingWidthRef.current;
        applySidebarWidthCssVar(pendingWidthRef.current);
      }
    };

    const handleMouseMove = (event) => {
      const deltaX = event.clientX - dragStartXRef.current;
      if (Math.abs(deltaX) > 2) movedRef.current = true;
      const nextWidth = Math.min(
        sidebarMaxWidth,
        Math.max(sidebarMinWidth, dragStartWidthRef.current + deltaX)
      );
      pendingWidthRef.current = nextWidth;
      if (rafRef.current !== null) return;
      rafRef.current = window.requestAnimationFrame(() => {
        rafRef.current = null;
        if (pendingWidthRef.current !== lastAppliedWidthRef.current) {
          lastAppliedWidthRef.current = pendingWidthRef.current;
          currentWidthRef.current = pendingWidthRef.current;
          applySidebarWidthCssVar(pendingWidthRef.current);
        }
      });
    };

    const handleMouseUp = () => {
      flushPendingWidth();
      setSidebarWidth?.(pendingWidthRef.current);
      const threshold = (sidebarMinWidth + sidebarMaxWidth) / 2;
      const shouldExpand = currentWidthRef.current >= threshold;
      setIsDragging(false);
      setIsSidebarDragging?.(false);
      if (typeof document !== "undefined") {
        document.body.style.userSelect = "";
        document.body.style.cursor = "";
      }
      if (!movedRef.current) {
        setIsSidebarCollapsed?.(!isSidebarCollapsed);
        return;
      }
      setIsSidebarCollapsed?.(!shouldExpand);
    };

    window.addEventListener("mousemove", handleMouseMove);
    window.addEventListener("mouseup", handleMouseUp);
    return () => {
      if (rafRef.current !== null && typeof window !== "undefined") {
        window.cancelAnimationFrame(rafRef.current);
        rafRef.current = null;
      }
      window.removeEventListener("mousemove", handleMouseMove);
      window.removeEventListener("mouseup", handleMouseUp);
    };
  }, [
    isDragging,
    isSidebarCollapsed,
    setIsSidebarCollapsed,
    setSidebarWidth,
    sidebarMaxWidth,
    sidebarMinWidth,
  ]);

  const handleResizeStart = (event) => {
    if (!setSidebarWidth || !setIsSidebarCollapsed) return;
    event.preventDefault();
    dragStartXRef.current = event.clientX;
    dragStartWidthRef.current = currentWidthRef.current;
    pendingWidthRef.current = currentWidthRef.current;
    lastAppliedWidthRef.current = currentWidthRef.current;
    movedRef.current = false;
    setIsDragging(true);
    setIsSidebarDragging?.(true);
    if (typeof document !== "undefined") {
      document.body.style.userSelect = "none";
      document.body.style.cursor = "ew-resize";
    }
  };

  let variantChange = isDragging ? "none" : "width 0.14s ease-out";
  let shadow = useColorModeValue(
    "14px 17px 40px 4px rgba(112, 144, 176, 0.08)",
    "unset"
  );
  // Chakra Color Mode
  let sidebarBg = useColorModeValue("white", "navy.800");
  let dragEdgeFade = useColorModeValue(
    "linear-gradient(to right, rgba(255,255,255,0) 0%, rgba(255,255,255,0.95) 100%)",
    "linear-gradient(to right, rgba(11,20,55,0) 0%, rgba(11,20,55,0.95) 100%)"
  );
  let sidebarMargins = "0px";

  // SIDEBAR
  return (
    <Box display={{ sm: "none", xl: "block" }} w="100%" position='fixed' minH='100%'>
      <Box
        bg={sidebarBg}
        transition={variantChange}
        w={`var(--admin-sidebar-width, ${sidebarWidth}px)`}
        h='100vh'
        m={sidebarMargins}
        minH='100%'
        overflowX='visible'
        position='relative'
        boxShadow={isDragging ? "none" : shadow}
        willChange='width'>
        <Box
          w='100%'
          h='100%'
          overflow='hidden'
        >
          {!isSidebarCollapsed || isDragging ? (
            <Box w={`${sidebarMaxWidth}px`} minW={`${sidebarMaxWidth}px`} maxW={`${sidebarMaxWidth}px`} h='100%'>
              <Scrollbars
                autoHide
                renderTrackVertical={renderTrack}
                renderThumbVertical={renderThumb}
                renderView={renderView}>
                <Content routes={routes} collapsed={false} />
              </Scrollbars>
            </Box>
          ) : null}
        </Box>
        {isDragging ? (
          <Box
            position='absolute'
            top='0'
            right='18px'
            h='100%'
            w='22px'
            pointerEvents='none'
            bg={dragEdgeFade}
          />
        ) : null}
        {setIsSidebarCollapsed ? (
          <Box
            role='separator'
            aria-label='Sidebar resize handle'
            aria-orientation='vertical'
            position='absolute'
            top='0'
            right='-18px'
            h='100%'
            w='28px'
            zIndex={20}
            cursor='ew-resize'
            onMouseDown={handleResizeStart}
            bg='transparent'
          >
            <Box
              position='absolute'
              top='50%'
              right='0'
              transform='translateY(-50%)'
              w='16px'
              h='84px'
              bg={isDragging ? 'rgba(236, 72, 153, 0.58)' : 'rgba(236, 72, 153, 0.38)'}
              clipPath='polygon(0 0, 74% 12%, 74% 88%, 0 100%)'
              borderTopRightRadius='0'
              borderBottomRightRadius='0'
              boxShadow='none'
              transition='background-color 0.15s ease'
              pointerEvents='none'
            />
          </Box>
        ) : null}
      </Box>
    </Box>
  );
}

// FUNCTIONS
export function SidebarResponsive(props) {
  let sidebarBackgroundColor = useColorModeValue("white", "navy.800");
  let menuColor = useColorModeValue("gray.400", "white");
  // // SIDEBAR
  const { isOpen, onOpen, onClose } = useDisclosure();
  const btnRef = React.useRef();

  const { routes } = props;
  // let isWindows = navigator.platform.startsWith("Win");
  //  BRAND

  return (
    <Flex display={{ sm: "flex", xl: "none" }} alignItems='center'>
      <Flex ref={btnRef} w='max-content' h='max-content' onClick={onOpen}>
        <Icon
          as={IoMenuOutline}
          color={menuColor}
          my='auto'
          w='20px'
          h='20px'
          me='10px'
          _hover={{ cursor: "pointer" }}
        />
      </Flex>
      <Drawer
        isOpen={isOpen}
        onClose={onClose}
        placement={document.documentElement.dir === "rtl" ? "right" : "left"}
        finalFocusRef={btnRef}>
        <DrawerOverlay />
        <DrawerContent w='285px' maxW='285px' bg={sidebarBackgroundColor}>
          <DrawerCloseButton
            zIndex='3'
            onClose={onClose}
            _focus={{ boxShadow: "none" }}
            _hover={{ boxShadow: "none" }}
          />
          <DrawerBody maxW='285px' px='0rem' pb='0'>
            <Scrollbars
              autoHide
              renderTrackVertical={renderTrack}
              renderThumbVertical={renderThumb}
              renderView={renderView}>
              <Content routes={routes} collapsed={false} />
            </Scrollbars>
          </DrawerBody>
        </DrawerContent>
      </Drawer>
    </Flex>
  );
}
// PROPS

Sidebar.propTypes = {
  logoText: PropTypes.string,
  routes: PropTypes.arrayOf(PropTypes.object),
  variant: PropTypes.string,
};

export default Sidebar;

