/*
 * can2usbhid.h
 *
 *  Created on: Aug 22, 2025
 *      Author: Hoang
 */

#ifndef INC_CAN2USBHID_H_
#define INC_CAN2USBHID_H_

#include "stm32f4xx_hal.h"
#include "can.h"
#include "usbd_hid_custom_if.h"
#include "math.h"
#include "float.h"
#include "tim.h"
#include "usb_device.h"
#include "usbhid2can.h"



uint8_t Can2Usb_Tranfer(HID_FrameFIFO_t *fifo, uint8_t *dest_buf);



#endif /* INC_CAN2USBHID_H_ */
