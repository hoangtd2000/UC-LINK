/*
 * usbhid2can.h
 *
 *  Created on: Aug 22, 2025
 *      Author: Hoang
 */

#ifndef INC_USBHID2CAN_H_
#define INC_USBHID2CAN_H_

#include "stm32f4xx_hal.h"
#include "can.h"
#include "usbd_hid_custom_if.h"
#include "math.h"
#include "float.h"
#include "tim.h"
#include "usb_device.h"

extern USBD_HandleTypeDef hUsbDevice;

#define HEADER_CAN_CONFIGURATION 0x01U
#define HEADER_CAN_CONFIGURATION 0x02U
#define HEADER_CAN_SEND_MESSAGE  0x03U

#define HID_FRAME_SIZE        32
#define HID_FRAME_BUFFER_SIZE 512
typedef struct {
    uint8_t frame[HID_FRAME_BUFFER_SIZE][HID_FRAME_SIZE];
    volatile uint8_t head;
    volatile uint8_t tail;
} HID_FrameFIFO_t;

uint8_t HID_Frame_Write(HID_FrameFIFO_t *fifo, uint8_t *data);
uint8_t HID_Frame_Read(HID_FrameFIFO_t *fifo, uint8_t *dest_buf);

uint8_t Usb2Can_Tranfer(HID_FrameFIFO_t *fifo);
//extern uint8_t (*FuncSendCanArray[3])(uint8_t *data);

#endif /* INC_USBHID2CAN_H_ */
