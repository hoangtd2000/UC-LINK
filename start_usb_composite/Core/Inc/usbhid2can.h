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

#define INTERNAL_CAN_IT_FLAGS          (  CAN_IT_RX_FIFO0_MSG_PENDING |\
                                          CAN_IT_ERROR_WARNING |\
                                          CAN_IT_ERROR_PASSIVE |\
                                          CAN_IT_BUSOFF |\
                                          CAN_IT_LAST_ERROR_CODE |\
                                          CAN_IT_ERROR )




#define HEADER_CAN_CONFIGURATION 0x01U
#define HEADER_CAN_TX_MESSAGE 0x02U
#define HEADER_CAN_RX_MESSAGE  0x03U

#define HID_FRAME_SIZE        32
#define HID_FRAME_BUFFER_SIZE 512
#define HID_FRAME_BUFFER_SIZE1 1024//2048U
typedef struct {
    uint8_t frame[HID_FRAME_BUFFER_SIZE][HID_FRAME_SIZE];
    volatile uint8_t head;
    volatile uint8_t tail;
} HID_FrameFIFO_t;

uint8_t HID_Frame_Write1(HID_FrameFIFO_t *fifo, uint8_t *data);
uint8_t HID_Frame_Write(HID_FrameFIFO_t *fifo, uint8_t *data);
uint8_t HID_Frame_Read(HID_FrameFIFO_t *fifo, uint8_t *dest_buf);

HAL_StatusTypeDef Usb2Can_Tranfer(HID_FrameFIFO_t *fifo);
//extern uint8_t (*FuncSendCanArray[3])(uint8_t *data);

#endif /* INC_USBHID2CAN_H_ */
