import { extendTheme } from "@chakra-ui/react";
import { CardComponent } from "@horizon/theme/additions/card/card";
import { buttonStyles } from "@horizon/theme/components/button";
import { badgeStyles } from "@horizon/theme/components/badge";
import { inputStyles } from "@horizon/theme/components/input";
import { progressStyles } from "@horizon/theme/components/progress";
import { sliderStyles } from "@horizon/theme/components/slider";
import { textareaStyles } from "@horizon/theme/components/textarea";
import { switchStyles } from "@horizon/theme/components/switch";
import { linkStyles } from "@horizon/theme/components/link";
import { breakpoints } from "@horizon/theme/foundations/breakpoints";
import { globalStyles } from "@horizon/theme/styles";
export default extendTheme(
  { breakpoints }, // Breakpoints
  globalStyles,
  badgeStyles, // badge styles
  buttonStyles, // button styles
  linkStyles, // link styles
  progressStyles, // progress styles
  sliderStyles, // slider styles
  inputStyles, // input styles
  textareaStyles, // textarea styles
  switchStyles, // switch styles
  CardComponent // card component
);

